package app.moji.android.sync

import app.moji.android.data.*
import app.moji.android.security.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.Serializable
import kotlinx.serialization.encodeToString
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.util.concurrent.TimeUnit

class GitHubSync(private val http: OkHttpClient = OkHttpClient.Builder().callTimeout(50, TimeUnit.SECONDS).build(), private val api: String = "https://api.github.com") {
    @Serializable private data class RemoteFile(val sha: String = "", val content: String? = null, val size: Long = 0)
    private fun request(path: String, token: String) = Request.Builder().url(api + path).header("Accept", "application/vnd.github+json").header("Authorization", "Bearer $token").header("User-Agent", "Moji-Android/1.0").header("X-GitHub-Api-Version", "2022-11-28")
    private fun error(code: Int): Nothing = throw IllegalStateException(when (code) { 401 -> "GitHub Token 已失效"; 403 -> "仓库权限不足或请求受限，请检查 Contents 读写权限"; 404 -> "仓库不存在，或当前 Token 无权访问"; 409, 422 -> "云端刚刚发生变化，请重新同步"; else -> "GitHub 请求失败（$code）" })
    suspend fun synchronize(local: Workspace): Pair<Workspace, Int> = withContext(Dispatchers.IO) {
        val config = local.sync
        require(config.token.isNotBlank()) { "请先填写 GitHub Token" }
        require(config.code.matches(Regex("[A-Za-z0-9]{12,128}"))) { "同步码需为 12–128 位字母或数字" }
        require(config.repository.matches(Regex("[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+"))) { "仓库格式应为 用户名/仓库名" }
        val path = "/repos/${config.repository}/contents/${dataPath(config.code)}"; var sha: String? = null
        var merged = MergeResult(local.notebooks, local.notes, 0)
        http.newCall(request(path, config.token).get().build()).execute().use { response ->
            if (response.isSuccessful) {
                val file = json.decodeFromString<RemoteFile>(response.body?.string() ?: error("云端文件为空")); sha = file.sha
                require(file.size <= 30_000_000) { "云端文件过大" }
                val encoded = file.content?.takeIf { it.isNotBlank() } ?: http.newCall(request("/repos/${config.repository}/git/blobs/${file.sha}", config.token).get().build()).execute().use { blob -> if (!blob.isSuccessful) error(blob.code); json.decodeFromString<RemoteFile>(blob.body!!.string()).content ?: error("云端文件为空") }
                val envelope = VaultStore.decodeEnvelope(Crypto.unb64(encoded).toString(Charsets.UTF_8))
                Crypto.derive(config.code, envelope.salt, envelope.iterations).use { key -> val remote = Crypto.decrypt<SyncPayload>(envelope, key); local.copy(schema = remote.schema, notebooks = remote.notebooks, notes = remote.notes).validate(); merged = WorkspaceMerge.merge(local, remote, config.base) }
            } else if (response.code == 404) {
                http.newCall(request("/repos/${config.repository}", config.token).get().build()).execute().use { if (!it.isSuccessful) error(it.code) }
            } else error(response.code)
        }
        val result = local.copy(notebooks = merged.notebooks, notes = merged.notes)
        val content = Crypto.newKey(config.code).use { json.encodeToString(Crypto.encrypt(result.payload(), it)) }
        require(content.length < 25_000_000) { "云同步文件超过容量，请先导出较大的图片" }
        val body = kotlinx.serialization.json.buildJsonObject {
            put("message", kotlinx.serialization.json.JsonPrimitive("moji: sync encrypted workspace")); put("content", kotlinx.serialization.json.JsonPrimitive(Crypto.b64(content.toByteArray(Charsets.UTF_8)))); sha?.let { put("sha", kotlinx.serialization.json.JsonPrimitive(it)) }
        }
        http.newCall(request(path, config.token).put(body.toString().toRequestBody("application/json".toMediaType())).build()).execute().use { if (!it.isSuccessful) error(it.code) }
        result.copy(sync = config.copy(lastSync = now(), base = WorkspaceMerge.revisions(result))) to merged.conflicts
    }
    companion object { fun dataPath(code: String) = "moji-notes/${Crypto.hash(code).take(40)}.moji" }
}
