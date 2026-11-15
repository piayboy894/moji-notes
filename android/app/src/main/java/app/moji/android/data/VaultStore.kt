package app.moji.android.data

import app.moji.android.security.Crypto
import app.moji.android.security.Envelope
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
    val file get() = File(directory, "workspace.json")
    val legacyFile get() = File(directory, "vault.moji")
    var recoveredBackup = false; private set
    suspend fun read(): Workspace? = withContext(Dispatchers.IO) {
        recoveredBackup = false
        val backup = File(file.path + ".bak")
        if (!file.exists() && !backup.exists()) return@withContext null
        try { readWorkspace(file) } catch (e: Exception) { if (!backup.exists()) throw e; recoveredBackup = true; readWorkspace(backup) }
    }
    suspend fun readLegacy(): Envelope = withContext(Dispatchers.IO) {
        try { readEnvelope(legacyFile) } catch (e: Exception) { val backup = File(legacyFile.path + ".bak"); if (!backup.exists()) throw e; readEnvelope(backup) }
    }
    suspend fun save(state: Workspace) = mutex.withLock {
        state.validate()
        withContext(Dispatchers.IO) {
            directory.mkdirs(); val content = json.encodeToString(state); require(content.toByteArray().size < 100_000_000) { "本地数据过大，请先导出图片" }
            val tmp = File(file.path + ".tmp")
            FileOutputStream(tmp).use { out -> out.write(content.toByteArray(Charsets.UTF_8)); out.fd.sync() }
            if (file.exists()) {
                val backupTmp = File(file.path + ".bak.tmp"); file.copyTo(backupTmp, overwrite = true)
                Files.move(backupTmp.toPath(), File(file.path + ".bak").toPath(), StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE)
            }
            Files.move(tmp.toPath(), file.toPath(), StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE)
        }
    }
    companion object {
        private fun readWorkspace(file: File): Workspace { require(file.length() < 100_000_000) { "本地文件过大" }; return json.decodeFromString<Workspace>(file.readText()).validate() }
        fun readEnvelope(file: File): Envelope { require(file.length() < 100_000_000) { "加密文件过大" }; return decodeEnvelope(file.readText()) }
        fun decodeEnvelope(text: String): Envelope = json.decodeFromString<Envelope>(text).also(Crypto::validate)
    }
}
