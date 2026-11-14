package app.moji.android.security

import java.nio.ByteBuffer
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

object Totp {
    fun code(secret: String, milliseconds: Long = System.currentTimeMillis()): String {
        val clean = secret.uppercase().replace(Regex("[\\s=-]"), "")
        require(clean.matches(Regex("[A-Z2-7]{16,128}"))) { "动态验证码密钥格式不正确" }
        val bits = clean.map { "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".indexOf(it).toString(2).padStart(5, '0') }.joinToString("")
        val key = ByteArray(bits.length / 8) { bits.substring(it * 8, it * 8 + 8).toInt(2).toByte() }
        return try { val mac = Mac.getInstance("HmacSHA1"); mac.init(SecretKeySpec(key, "HmacSHA1")); val bytes = mac.doFinal(ByteBuffer.allocate(8).putLong(milliseconds / 30000).array()); val offset = bytes.last().toInt() and 15; val value = ((bytes[offset].toInt() and 127) shl 24) or ((bytes[offset + 1].toInt() and 255) shl 16) or ((bytes[offset + 2].toInt() and 255) shl 8) or (bytes[offset + 3].toInt() and 255); (value % 1_000_000).toString().padStart(6, '0') } finally { key.fill(0) }
    }
}
