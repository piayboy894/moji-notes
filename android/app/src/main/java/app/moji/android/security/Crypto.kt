package app.moji.android.security

import app.moji.android.data.json
import kotlinx.serialization.Serializable
import kotlinx.serialization.encodeToString
import java.security.MessageDigest
import java.security.SecureRandom
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.SecretKeySpec

@Serializable data class Envelope(val format: String = "moji-encrypted", val version: Int = 1, val salt: String, val iv: String, val data: String, val iterations: Int = 310_000)
class VaultKey(val bytes: ByteArray, val salt: String, val iterations: Int = 310_000) : AutoCloseable { override fun close() { bytes.fill(0) } }
object Crypto {
    const val ITERATIONS = 310_000
    private val random = SecureRandom()
    fun b64(value: ByteArray): String = Base64.getEncoder().encodeToString(value)
    fun unb64(value: String): ByteArray = Base64.getMimeDecoder().decode(value)
    fun newKey(password: String) = derive(password, b64(ByteArray(16).also(random::nextBytes)), ITERATIONS)
    fun derive(password: String, salt: String, iterations: Int = ITERATIONS): VaultKey {
        require(iterations in 100_000..2_000_000) { "加密参数无效" }
        val saltBytes = unb64(salt); require(saltBytes.size == 16) { "加密文件已损坏" }
        val spec = PBEKeySpec(password.toCharArray(), saltBytes, iterations, 256)
        return try { VaultKey(SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256").generateSecret(spec).encoded, salt, iterations) } finally { spec.clearPassword() }
    }
    fun validate(e: Envelope) {
        require(e.format == "moji-encrypted" && e.version == 1 && e.iterations in 100_000..2_000_000 && unb64(e.salt).size == 16 && unb64(e.iv).size == 12 && unb64(e.data).size >= 16) { "这不是有效的墨记加密文件" }
    }
    fun encryptText(plain: String, key: VaultKey): Envelope {
        val bytes = plain.toByteArray(Charsets.UTF_8); val iv = ByteArray(12).also(random::nextBytes)
        return try { val cipher = Cipher.getInstance("AES/GCM/NoPadding"); cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key.bytes, "AES"), GCMParameterSpec(128, iv)); Envelope(salt = key.salt, iv = b64(iv), data = b64(cipher.doFinal(bytes)), iterations = key.iterations) } finally { bytes.fill(0) }
    }
    fun decryptText(e: Envelope, key: VaultKey): String {
        validate(e)
        val bytes = try { val cipher = Cipher.getInstance("AES/GCM/NoPadding"); cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(key.bytes, "AES"), GCMParameterSpec(128, unb64(e.iv))); cipher.doFinal(unb64(e.data)) } catch (_: java.security.GeneralSecurityException) { throw IllegalArgumentException("密码不正确，或加密文件已损坏") }
        return try { bytes.toString(Charsets.UTF_8) } finally { bytes.fill(0) }
    }
    inline fun <reified T> encrypt(value: T, key: VaultKey) = encryptText(json.encodeToString(value), key)
    inline fun <reified T> decrypt(envelope: Envelope, key: VaultKey): T = json.decodeFromString(decryptText(envelope, key))
    fun hash(value: String) = MessageDigest.getInstance("SHA-256").digest(value.toByteArray(Charsets.UTF_8)).joinToString("") { "%02x".format(it) }
    fun password(length: Int = 20, symbols: Boolean = true): String {
        require(length in 12..128)
        val groups = listOf("ABCDEFGHJKLMNPQRSTUVWXYZ", "abcdefghijkmnopqrstuvwxyz", "23456789") + if (symbols) listOf("!@#$%&*+-=?") else emptyList()
        val all = groups.joinToString(""); val values = groups.map { it[random.nextInt(it.length)] }.toMutableList()
        repeat(length - values.size) { values += all[random.nextInt(all.length)] }
        for (i in values.lastIndex downTo 1) { val j = random.nextInt(i + 1); val c = values[i]; values[i] = values[j]; values[j] = c }
        return values.joinToString("")
    }
}
