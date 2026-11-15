package app.moji.android.sync

import app.moji.android.data.*
import app.moji.android.security.Crypto
import app.moji.android.security.Envelope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.Serializable
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.*
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.MediaType.Companion.toMediaType
import java.net.URLEncoder
import java.util.concurrent.TimeUnit

@Serializable data class SaveEntry(val id: String, val name: String, val savedAt: Long, val bookCount: Int, val noteCount: Int, val blobSha: String, val platform: String, val legacy: Boolean = false)
@Serializable data class SaveIndex(val format: String, val version: Int, val entries: List<SaveEntry>)
data class SaveCatalog(val source: String, val branch: String, val headSha: String?, val treeSha: String?, val entries: List<SaveEntry>)
object GameSaves {
    fun capture(workspace: Workspace): SyncPayload { workspace.validate(); return json.decodeFromString(json.encodeToString(workspace.payload())) }
    fun apply(local: Workspace, save: SyncPayload): Workspace = local.copy(schema = save.schema, id = save.id, notebooks = save.notebooks, notes = save.notes, preferences = local.preferences.copy(autoSync = false)).validate()
    fun validate(index: SaveIndex) {
        require(index.format == "moji-saves" && index.version == 1 && index.entries.size <= 500 && index.entries.map { it.id }.distinct().size == index.entries.size) { "云存档目录损坏或版本不受支持" }
        require(index.entries.all { it.id.matches(Regex("[a-f0-9]{32}")) && it.name.isNotBlank() && it.name.length <= 60 && it.savedAt in 1..253402300799999 && it.bookCount in 0..5000 && it.noteCount in 0..100000 && it.blobSha.matches(Regex("[a-f0-9]{40}")) && !it.legacy }) { "云存档信息损坏" }
    }
}

// A user action atomically commits encrypted data + catalog. No background sync,
// automatic merge, forced overwrite, retry, or selection of a newer save.
class CloudSaveService(private val http: OkHttpClient = OkHttpClient.Builder().callTimeout(60, TimeUnit.SECONDS).build(), private val apiBase: String = "https://api.github.com") {
    private fun source(c: SyncSettings) = Crypto.hash(c.repository.lowercase() + "\n" + c.code)
    private fun encoded(value: String) = URLEncoder.encode(value, "UTF-8").replace("+", "%20")
    private fun checkSource(c: SyncSettings, catalog: SaveCatalog) { validateConfig(c); require(catalog.source == source(c)) { "连接已更改，请刷新存档列表" } }
    private fun api(c: SyncSettings, method: String, path: String, body: JsonObject? = null, missing: Boolean = false): JsonObject? {
        val request = Request.Builder().url("$apiBase/repos/${c.repository}$path").header("User-Agent", "Moji-Android/1.0").header("Accept", "application/vnd.github+json").header("Authorization", "Bearer ${c.token}").header("X-GitHub-Api-Version", "2022-11-28")
            .method(method, if (method in listOf("POST", "PATCH", "PUT")) (body ?: buildJsonObject {}).toString().toRequestBody("application/json".toMediaType()) else null).build()
        return http.newCall(request).execute().use { response ->
            if (missing && (response.code == 404 || (response.code == 409 && path.startsWith("/git/ref/heads/")))) return@use null
            require(response.isSuccessful) { when (response.code) { 401 -> "GitHub Token 已失效"; 403 -> "仓库权限不足或请求受限，请检查 Contents 读写权限"; 404 -> "仓库或存档不存在，或当前 Token 无权访问"; 409, 422 -> "云端已变化，请刷新列表后重新确认操作"; else -> "GitHub 请求失败（${response.code}）" } }
            val text = response.body?.string().orEmpty(); if (text.isBlank()) buildJsonObject {} else json.parseToJsonElement(text).jsonObject
        }
    }
    private fun decodeFile(c: SyncSettings, file: JsonObject): String {
        require((file["size"]?.jsonPrimitive?.long ?: 0) <= 30_000_000) { "存档超过容量限制" }
        val content = file["content"]?.jsonPrimitive?.contentOrNull
        if (!content.isNullOrEmpty()) return Crypto.unb64(content).toString(Charsets.UTF_8)
        val blob = api(c, "GET", "/git/blobs/" + file.getValue("sha").jsonPrimitive.content)!!
        require(blob.getValue("size").jsonPrimitive.long <= 30_000_000) { "存档超过容量限制" }
        return Crypto.unb64(blob.getValue("content").jsonPrimitive.content).toString(Charsets.UTF_8)
    }
    private inline fun <reified T> decrypt(text: String, code: String): T { val e = json.decodeFromString<Envelope>(text); return Crypto.derive(code, e.salt, e.iterations).use { Crypto.decrypt<T>(e, it) } }
    private inline fun <reified T> encrypt(value: T, code: String): String { val text = Crypto.newKey(code).use { json.encodeToString(Crypto.encrypt(value, it)) }; require(text.length <= 25_000_000) { "存档超过容量限制，请先导出较大的图片" }; return text }
    suspend fun list(config: SyncSettings): SaveCatalog = withContext(Dispatchers.IO) {
        validateConfig(config)
        val repo = api(config, "GET", "")!!; val branch = repo["default_branch"]?.jsonPrimitive?.contentOrNull ?: "main"
        val head = api(config, "GET", "/git/ref/heads/${encoded(branch)}", missing = true)
        if (head == null) return@withContext SaveCatalog(source(config), branch, null, null, emptyList())
        val headSha = head.getValue("object").jsonObject.getValue("sha").jsonPrimitive.content
        val commit = api(config, "GET", "/git/commits/$headSha")!!; val tree = commit.getValue("tree").jsonObject.getValue("sha").jsonPrimitive.content
        val indexFile = api(config, "GET", "/contents/${root(config.code)}index.moji?ref=$headSha", missing = true)
        val entries = mutableListOf<SaveEntry>()
        if (indexFile != null) { val index = decrypt<SaveIndex>(decodeFile(config, indexFile), config.code); GameSaves.validate(index); entries += index.entries }
        val legacy = api(config, "GET", "/contents/${legacyPath(config.code)}?ref=$headSha", missing = true)
        if (legacy != null) { val payload = decrypt<SyncPayload>(decodeFile(config, legacy), config.code); val w = GameSaves.apply(Workspace(), payload); entries += SaveEntry("legacy", "旧版存档", 0, w.liveBooks.size, w.liveNotes.size, legacy.getValue("sha").jsonPrimitive.content, "旧版", true) }
        SaveCatalog(source(config), branch, headSha, tree, entries.sortedByDescending { it.savedAt })
    }
    suspend fun read(config: SyncSettings, catalog: SaveCatalog, entry: SaveEntry): SyncPayload = withContext(Dispatchers.IO) {
        checkSource(config, catalog); require(entry in catalog.entries) { "请重新选择存档" }
        val blob = api(config, "GET", "/git/blobs/${entry.blobSha}")!!; val payload = decrypt<SyncPayload>(decodeFile(config, blob), config.code)
        GameSaves.apply(Workspace(), payload); payload
    }
    private fun blob(c: SyncSettings, text: String): String = api(c, "POST", "/git/blobs", buildJsonObject { put("content", Crypto.b64(text.toByteArray(Charsets.UTF_8))); put("encoding", "base64") })!!.getValue("sha").jsonPrimitive.content
    suspend fun write(config: SyncSettings, original: SaveCatalog, workspace: Workspace, name: String, overwrite: SaveEntry? = null): SaveCatalog = withContext(Dispatchers.IO) {
        checkSource(config, original); val title = name.trim(); require(title.isNotEmpty() && title.length <= 60) { "存档名称需为 1–60 个字符" }
        require(overwrite == null || overwrite in original.entries) { "请重新选择存档" }
        require(overwrite != null || original.entries.count { !it.legacy } < 500) { "存档已达 500 份，请先删除不需要的存档" }
        var catalog = original
        if (catalog.headSha == null) {
            val emptyIndex = encrypt(SaveIndex("moji-saves", 1, emptyList()), config.code)
            api(config, "PUT", "/contents/${root(config.code)}index.moji", buildJsonObject { put("message", "moji: initialize encrypted saves"); put("content", Crypto.b64(emptyIndex.toByteArray(Charsets.UTF_8))) })
            catalog = list(config)
        }
        val payload = GameSaves.capture(workspace); val dataSha = blob(config, encrypt(payload, config.code))
        val entry = SaveEntry(overwrite?.id ?: uid(), if (overwrite?.legacy == true) "旧版存档" else title, now(), workspace.liveBooks.size, workspace.liveNotes.size, dataSha, "Android", overwrite?.legacy ?: false)
        commit(config, catalog, (catalog.entries.filter { it.id != entry.id } + entry).sortedByDescending { it.savedAt }, entry, false)
    }
    suspend fun delete(config: SyncSettings, catalog: SaveCatalog, entry: SaveEntry): SaveCatalog = withContext(Dispatchers.IO) {
        checkSource(config, catalog); require(entry in catalog.entries) { "请重新选择存档" }
        commit(config, catalog, catalog.entries.filter { it.id != entry.id }, entry, true)
    }
    private fun commit(c: SyncSettings, catalog: SaveCatalog, entries: List<SaveEntry>, changed: SaveEntry, deleting: Boolean): SaveCatalog {
        val index = SaveIndex("moji-saves", 1, entries.filter { !it.legacy }); GameSaves.validate(index); val indexSha = blob(c, encrypt(index, c.code))
        val changes = buildJsonArray {
            add(buildJsonObject { put("path", "${root(c.code)}index.moji"); put("mode", "100644"); put("type", "blob"); put("sha", indexSha) })
            add(buildJsonObject { put("path", if (changed.legacy) legacyPath(c.code) else "${root(c.code)}${changed.id}.moji"); put("mode", "100644"); put("type", "blob"); put("sha", if (deleting) JsonNull else JsonPrimitive(changed.blobSha)) })
        }
        val treeSha = api(c, "POST", "/git/trees", buildJsonObject { put("tree", changes); catalog.treeSha?.let { put("base_tree", it) } })!!.getValue("sha").jsonPrimitive.content
        val commitSha = api(c, "POST", "/git/commits", buildJsonObject { put("message", if (deleting) "moji: delete encrypted save" else "moji: write encrypted save"); put("tree", treeSha); put("parents", buildJsonArray { catalog.headSha?.let { add(JsonPrimitive(it)) } }) })!!.getValue("sha").jsonPrimitive.content
        if (catalog.headSha == null) api(c, "POST", "/git/refs", buildJsonObject { put("ref", "refs/heads/${catalog.branch}"); put("sha", commitSha) })
        else api(c, "PATCH", "/git/refs/heads/${encoded(catalog.branch)}", buildJsonObject { put("sha", commitSha); put("force", false) })
        return catalog.copy(headSha = commitSha, treeSha = treeSha, entries = entries)
    }
    companion object {
        fun root(code: String) = "moji-notes/${Crypto.hash(code).take(40)}/"
        fun legacyPath(code: String) = "moji-notes/${Crypto.hash(code).take(40)}.moji"
        fun validateConfig(c: SyncSettings) { require(c.token.isNotBlank()) { "请先设置 GitHub Token" }; require(c.code.matches(Regex("[A-Za-z0-9]{12,128}"))) { "存档码需为 12–128 位字母或数字" }; require(c.repository.matches(Regex("[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+"))) { "仓库格式应为 用户名/仓库名" } }
    }
}
