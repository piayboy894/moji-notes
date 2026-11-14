package app.moji.android.data

import app.moji.android.security.Crypto
import app.moji.android.security.Envelope
import app.moji.android.security.VaultKey
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import kotlinx.serialization.encodeToString
import java.io.File
import java.io.FileOutputStream
import java.nio.file.Files
import java.nio.file.StandardCopyOption

class VaultStore(private val directory: File) {
    private val mutex = Mutex()
    val file get() = File(directory, "vault.moji")
    var recoveredBackup = false; private set
    suspend fun read(): Envelope? = withContext(Dispatchers.IO) {
        recoveredBackup = false
        if (!file.exists()) return@withContext null
        try { readEnvelope(file) } catch (e: Exception) { val backup = File(directory, "vault.moji.bak"); if (!backup.exists()) throw e; recoveredBackup = true; readEnvelope(backup) }
    }
    suspend fun save(state: Workspace, key: VaultKey) = mutex.withLock {
        withContext(Dispatchers.IO) {
            directory.mkdirs(); val content = json.encodeToString(Crypto.encrypt(state, key)); require(content.length < 100_000_000) { "本地数据过大，请先导出图片" }
            val tmp = File(directory, "vault.moji.tmp")
            FileOutputStream(tmp).use { out -> out.write(content.toByteArray(Charsets.UTF_8)); out.fd.sync() }
            if (file.exists()) {
                val backupTmp = File(directory, "vault.moji.bak.tmp"); file.copyTo(backupTmp, overwrite = true)
                Files.move(backupTmp.toPath(), File(directory, "vault.moji.bak").toPath(), StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE)
            }
            Files.move(tmp.toPath(), file.toPath(), StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE)
        }
    }
    companion object {
        fun readEnvelope(file: File): Envelope { require(file.length() < 100_000_000) { "加密文件过大" }; return decodeEnvelope(file.readText()) }
        fun decodeEnvelope(text: String): Envelope = json.decodeFromString<Envelope>(text).also(Crypto::validate)
    }
}
