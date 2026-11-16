package app.moji.android.sync

import app.moji.android.data.json
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import okhttp3.Call
import okhttp3.Callback
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import java.io.IOException
import java.net.SocketTimeoutException
import java.net.UnknownHostException
import java.util.concurrent.TimeUnit
import javax.net.ssl.SSLException
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

class UpdateCheckException(message: String, cause: Throwable? = null) : IOException(message, cause)

@Serializable private data class FeedAsset(val name: String, val url: String, val size: Long, val sha256: String)
@Serializable private data class Feed(val schema: Int, val platform: String, val version: String, @SerialName("release_url") val releaseUrl: String, val assets: List<FeedAsset>)
@Serializable private data class ReleaseAsset(val name: String, @SerialName("browser_download_url") val url: String, val size: Long, val digest: String? = null)
@Serializable private data class Release(@SerialName("tag_name") val tag: String, val draft: Boolean, val prerelease: Boolean, val assets: List<ReleaseAsset>)

internal class UpdateCatalog(
    client: OkHttpClient,
    private val feeds: List<String> = listOf("https://raw.githubusercontent.com/piayboy894/moji-notes/main/updates/android.json", "https://github.com/piayboy894/moji-notes/releases/download/update-channel/android.json"),
    private val api: String = "https://api.github.com/repos/piayboy894/moji-notes/releases?per_page=8"
) {
    private val http = client.newBuilder().callTimeout(8, TimeUnit.SECONDS).connectTimeout(5, TimeUnit.SECONDS).readTimeout(8, TimeUnit.SECONDS).build()

    suspend fun check(currentCode: Int): AndroidUpdate? = coroutineScope {
        val results = feeds.map { url -> async {
            try { Result.success(parseFeed(read(url, 65536))) }
            catch (cancel: CancellationException) { throw cancel }
            catch (error: Exception) { Result.failure(error) }
        } }.awaitAll()
        val valid = results.mapNotNull { it.getOrNull() }.maxByOrNull { it.versionCode }
        if (valid != null) return@coroutineScope valid.takeIf { it.versionCode > currentCode }
        val errors = results.mapNotNull { it.exceptionOrNull() }.toMutableList()
        try {
            json.decodeFromString<List<Release>>(read(api, 1048576))
                .filter { !it.draft && !it.prerelease && it.tag.startsWith("android-v") }
                .mapNotNull { release ->
                    val version = release.tag.removePrefix("android-v"); val code = AndroidUpdater.versionCode(version) ?: return@mapNotNull null
                    val name = "Moji-Notes-Android-$version.apk"
                    val asset = release.assets.firstOrNull { it.name == name } ?: return@mapNotNull null
                    val hash = asset.digest?.removePrefix("sha256:") ?: return@mapNotNull null
                    validate(version, asset.url, name, asset.size, hash)
                    AndroidUpdate(version, code, asset.url, asset.size, hash.lowercase(), releaseUrl(version))
                }.filter { it.versionCode > currentCode }.maxByOrNull { it.versionCode }
        } catch (cancel: CancellationException) { throw cancel }
        catch (error: Exception) { errors += error; throw failure(errors) }
    }

    private fun parseFeed(text: String): AndroidUpdate {
        val feed = json.decodeFromString<Feed>(text)
        require(feed.schema == 1 && feed.platform == "android")
        val code = AndroidUpdater.versionCode(feed.version) ?: error("Unsupported update version")
        require(feed.releaseUrl == releaseUrl(feed.version))
        val name = "Moji-Notes-Android-${feed.version}.apk"
        val asset = feed.assets.first { it.name == name }
        validate(feed.version, asset.url, name, asset.size, asset.sha256)
        return AndroidUpdate(feed.version, code, asset.url, asset.size, asset.sha256.lowercase(), feed.releaseUrl)
    }

    private fun validate(version: String, url: String, name: String, size: Long, hash: String) {
        require(url == "https://github.com/piayboy894/moji-notes/releases/download/android-v$version/$name" && size > 0 && hash.matches(Regex("[a-fA-F0-9]{64}")))
    }
    private fun releaseUrl(version: String) = "https://github.com/piayboy894/moji-notes/releases/tag/android-v$version"

    private class SourceError(val code: Int, val limited: Boolean) : IOException()
    private suspend fun read(url: String, maximum: Int): String {
        for (attempt in 0..1) {
            try {
                return get(url, maximum)
            } catch (cancel: CancellationException) { throw cancel }
            catch (error: IOException) {
                if (attempt == 1 || error is SocketTimeoutException || error is SSLException || (error is SourceError && error.code !in setOf(408, 500, 502, 503, 504))) throw error
                delay(250)
            }
        }
        error("unreachable")
    }

    private suspend fun get(url: String, maximum: Int): String = suspendCancellableCoroutine { continuation ->
        val call = http.newCall(Request.Builder().url(url).header("User-Agent", "Moji-Android").header("Accept", "application/json").header("Cache-Control", "no-cache").build())
        continuation.invokeOnCancellation { call.cancel() }
        call.enqueue(object : Callback {
            override fun onFailure(call: Call, error: IOException) { if (continuation.isActive) continuation.resumeWithException(error) }
            override fun onResponse(call: Call, response: Response) {
                try {
                    val text = response.use {
                        if (!it.isSuccessful) throw SourceError(it.code, it.code == 429 || (it.code == 403 && it.header("X-RateLimit-Remaining") == "0"))
                        val body = it.body ?: throw IOException("Empty update body")
                        require(body.contentLength() <= maximum)
                        body.byteStream().use { input ->
                            val output = java.io.ByteArrayOutputStream(); val buffer = ByteArray(8192); var count: Int
                            while (input.read(buffer).also { count = it } > 0) { require(output.size() + count <= maximum); output.write(buffer, 0, count) }
                            output.toString(Charsets.UTF_8.name()).removePrefix("\uFEFF")
                        }
                    }
                    if (continuation.isActive) continuation.resume(text)
                } catch (error: Exception) { if (continuation.isActive) continuation.resumeWithException(error) }
            }
        })
    }

    private fun failure(errors: List<Throwable>): UpdateCheckException {
        val message = when {
            errors.filterIsInstance<SourceError>().any { it.limited } -> "GitHub 请求暂时受限，请稍后重试。"
            errors.any { it is SSLException } -> "与更新服务器的安全连接失败，请检查系统时间或代理设置。"
            errors.any { it is UnknownHostException } -> "无法解析更新服务器地址，请检查 DNS 或代理设置。"
            errors.any { it is SocketTimeoutException || (it is java.io.InterruptedIOException && it.message == "timeout") } -> "更新服务响应超时，请稍后重试。"
            errors.lastOrNull() is SourceError -> "更新服务暂时不可用（HTTP ${(errors.last() as SourceError).code}），请稍后重试。"
            errors.lastOrNull() is kotlinx.serialization.SerializationException || errors.lastOrNull() is IllegalArgumentException || errors.lastOrNull() is NoSuchElementException -> "更新信息暂时无法读取，请稍后重试。"
            else -> "暂时无法连接更新服务器，可从 GitHub 下载新版。"
        }
        return UpdateCheckException(message, errors.lastOrNull())
    }
}
