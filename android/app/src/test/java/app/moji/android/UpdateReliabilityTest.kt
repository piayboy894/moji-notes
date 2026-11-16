package app.moji.android

import app.moji.android.data.UserMessages
import app.moji.android.sync.UpdateCatalog
import app.moji.android.sync.UpdateCheckException
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.*
import org.junit.Test
import java.util.concurrent.atomic.AtomicInteger

class UpdateReliabilityTest {
    private fun feed(version: String = "2.4.1", url: String = "https://github.com/piayboy894/moji-notes/releases/download/android-v$version/Moji-Notes-Android-$version.apk") = """{"schema":1,"platform":"android","version":"$version","release_url":"https://github.com/piayboy894/moji-notes/releases/tag/android-v$version","assets":[{"name":"Moji-Notes-Android-$version.apk","url":"$url","size":4,"sha256":"${"a".repeat(64)}"}]}"""
    private fun catalog(server: MockWebServer) = UpdateCatalog(OkHttpClient(), listOf(server.url("/first").toString(), server.url("/backup").toString()), server.url("/api").toString())
    private fun server(reply: (RecordedRequest) -> MockResponse) = MockWebServer().apply { dispatcher = object : Dispatcher() { override fun dispatch(request: RecordedRequest): MockResponse { assertNull(request.getHeader("Authorization")); return reply(request) } }; start() }

    @Test fun smallFeedsSelectNewestWithoutUsingApi() = runBlocking {
        server { r -> assertNotEquals("/api", r.path); MockResponse().setBody(feed(if (r.path == "/first") "2.4.0" else "2.4.1")) }.use { s ->
            val update = catalog(s).check(20300)!!; assertEquals(20401, update.versionCode); assertEquals("a".repeat(64), update.sha256)
        }
    }
    @Test fun failedPrimaryRetriesAndOtherFeedStillWorks() = runBlocking {
        val count = AtomicInteger()
        server { r -> assertNotEquals("/api", r.path); if (r.path == "/first") MockResponse().setResponseCode(404) else if (count.incrementAndGet() == 1) MockResponse().setResponseCode(503) else MockResponse().setBody(feed()) }.use { s ->
            assertEquals(20401, catalog(s).check(20300)!!.versionCode); assertEquals(2, count.get())
        }
    }
    @Test fun validCurrentFeedDoesNotUseApi() = runBlocking {
        server { r -> assertNotEquals("/api", r.path); MockResponse().setBody(feed()) }.use { s -> assertNull(catalog(s).check(20401)) }
    }
    @Test fun rateLimitHasSpecificMessageAndDoesNotMeanLatest() = runBlocking {
        server { r -> if (r.path == "/api") MockResponse().setResponseCode(403).setHeader("X-RateLimit-Remaining", "0") else MockResponse().setResponseCode(404) }.use { s ->
            try { catalog(s).check(20300); fail("Expected failure") } catch (e: UpdateCheckException) { assertTrue(UserMessages.forError(e).contains("受限")); assertFalse(UserMessages.forError(e).contains("检查网络")) }
        }
    }
    @Test fun untrustedFeedAndInvalidApiCannotClaimLatest() = runBlocking {
        server { r -> MockResponse().setBody(if (r.path == "/api") "invalid" else feed(url = "https://untrusted.invalid/app.apk")) }.use { s ->
            try { catalog(s).check(20300); fail("Expected failure") } catch (e: UpdateCheckException) { assertTrue(e.message!!.contains("无法读取")) }
        }
    }
    @Test fun legacyApiFallbackUsesHighestStableAndroidVersion() = runBlocking {
        fun release(version: String, prefix: String = "android-v", prerelease: Boolean = false) = """{"tag_name":"$prefix$version","draft":false,"prerelease":$prerelease,"assets":[{"name":"Moji-Notes-Android-$version.apk","browser_download_url":"https://github.com/piayboy894/moji-notes/releases/download/$prefix$version/Moji-Notes-Android-$version.apk","size":4,"digest":"sha256:${"b".repeat(64)}"}]}"""
        server { r -> if (r.path != "/api") MockResponse().setResponseCode(404) else MockResponse().setBody("[${release("2.0.0")},${release("2.4.1")},${release("9.0.0", "v")},${release("9.0.0", prerelease = true)}]") }.use { s -> assertEquals(20401, catalog(s).check(20300)!!.versionCode) }
    }
    @Test fun cancellingCheckDoesNotLaunchApiFallback() = runBlocking {
        server { r -> assertNotEquals("/api", r.path); MockResponse().setHeadersDelay(2, java.util.concurrent.TimeUnit.SECONDS).setBody(feed()) }.use { s ->
            val job = launch { catalog(s).check(20300) }; delay(100); job.cancel(); job.join(); assertTrue(job.isCancelled); assertTrue(s.requestCount <= 2)
        }
    }
}
