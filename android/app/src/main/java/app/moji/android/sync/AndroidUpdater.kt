package app.moji.android.sync

import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.content.FileProvider
import app.moji.android.BuildConfig
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.security.MessageDigest
import java.util.concurrent.TimeUnit

data class AndroidUpdate(val version: String, val versionCode: Int, val url: String, val size: Long, val sha256: String, val releaseUrl: String)
class AndroidUpdater(private val context: Context) {
    private val http = OkHttpClient.Builder().callTimeout(15, TimeUnit.MINUTES).build()
    private fun request(url: String) = Request.Builder().url(url).header("User-Agent", "Moji-Android/${BuildConfig.VERSION_NAME}").build()
    suspend fun check(): AndroidUpdate? = UpdateCatalog(http).check(BuildConfig.VERSION_CODE)
    suspend fun download(update: AndroidUpdate, progress: (Float) -> Unit): File = withContext(Dispatchers.IO) {
        val uri = android.net.Uri.parse(update.url)
        require(uri.scheme == "https" && uri.host == "github.com" && uri.path?.startsWith("/piayboy894/moji-notes/releases/download/android-v") == true) { "更新来源不受信任" }
        val directory = File(context.cacheDir, "updates").apply { mkdirs() }; val temporary = File(directory, "update.apk.tmp"); val destination = File(directory, "update.apk")
        try {
            http.newCall(request(update.url)).execute().use { response ->
                require(response.isSuccessful) { "更新下载失败（${response.code}）" }
                response.body!!.byteStream().use { source -> temporary.outputStream().use { out -> val buffer = ByteArray(81920); var total = 0L; var read: Int
                    while (source.read(buffer).also { read = it } >= 0) { total += read; require(total <= update.size + 1024) { "更新包大小异常" }; out.write(buffer, 0, read); progress((total.toFloat() / update.size.coerceAtLeast(1)).coerceIn(0f, 1f)) }
                    require(total == update.size) { "更新包下载不完整" }
                } }
            }
            val digest = MessageDigest.getInstance("SHA-256"); temporary.inputStream().use { stream -> val buffer = ByteArray(81920); var read: Int; while (stream.read(buffer).also { read = it } >= 0) digest.update(buffer, 0, read) }
            require(digest.digest().joinToString("") { "%02x".format(it) } == update.sha256) { "更新包校验失败" }
            verifyCertificate(temporary, update.versionCode)
            java.nio.file.Files.move(temporary.toPath(), destination.toPath(), java.nio.file.StandardCopyOption.REPLACE_EXISTING)
            destination
        } catch (error: Exception) { temporary.delete(); throw error }
    }
    @Suppress("DEPRECATION") private fun verifyCertificate(apk: File, expectedCode: Int) {
        val flags = if (Build.VERSION.SDK_INT >= 28) PackageManager.GET_SIGNING_CERTIFICATES else PackageManager.GET_SIGNATURES
        val archive = context.packageManager.getPackageArchiveInfo(apk.path, flags) ?: error("更新包无法解析")
        val installed = context.packageManager.getPackageInfo(context.packageName, flags)
        require(archive.packageName == context.packageName) { "更新包与当前应用不匹配" }
        val code = if (Build.VERSION.SDK_INT >= 28) archive.longVersionCode else archive.versionCode.toLong()
        require(code == expectedCode.toLong() && code > BuildConfig.VERSION_CODE) { "更新包版本不匹配" }
        val old = if (Build.VERSION.SDK_INT >= 28) installed.signingInfo?.apkContentsSigners else installed.signatures
        val fresh = if (Build.VERSION.SDK_INT >= 28) archive.signingInfo?.apkContentsSigners else archive.signatures
        require(!old.isNullOrEmpty() && !fresh.isNullOrEmpty() && fresh.all { f -> old.any { MessageDigest.isEqual(it.toByteArray(), f.toByteArray()) } }) { "更新包签名与当前应用不同，已取消安装" }
    }
    fun installIntent(file: File): Intent = Intent(Intent.ACTION_VIEW).apply {
        setDataAndType(FileProvider.getUriForFile(context, "${context.packageName}.files", file), "application/vnd.android.package-archive"); addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
    }
    companion object {
        fun versionCode(version: String): Int? { val match = Regex("(\\d{1,3})\\.(\\d{1,2})\\.(\\d{1,2})").matchEntire(version) ?: return null; val (major, minor, patch) = match.destructured; return major.toInt() * 10000 + minor.toInt() * 100 + patch.toInt() }
    }
}
