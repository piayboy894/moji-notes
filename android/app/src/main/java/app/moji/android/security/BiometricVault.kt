package app.moji.android.security

import android.content.Context
import android.os.Build
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.core.content.ContextCompat
import androidx.fragment.app.FragmentActivity
import kotlinx.serialization.Serializable
import app.moji.android.data.json
import kotlinx.serialization.encodeToString
import java.io.File
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

class BiometricVault(private val context: Context) {
    @Serializable private data class WrappedKey(val iv: String, val data: String, val salt: String, val iterations: Int)
    private val file get() = File(context.noBackupFilesDir, "biometric-key.json")
    val enabled get() = file.exists()
    private val authenticators get() = if (Build.VERSION.SDK_INT >= 30) BiometricManager.Authenticators.BIOMETRIC_STRONG or BiometricManager.Authenticators.DEVICE_CREDENTIAL else BiometricManager.Authenticators.BIOMETRIC_STRONG
    fun available() = BiometricManager.from(context).canAuthenticate(authenticators) == BiometricManager.BIOMETRIC_SUCCESS
    private fun key(): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (store.getKey(ALIAS, null) as? SecretKey)?.let { return it }
        val builder = KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT).setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).setUserAuthenticationRequired(true)
        if (Build.VERSION.SDK_INT >= 30) builder.setUserAuthenticationParameters(0, KeyProperties.AUTH_BIOMETRIC_STRONG or KeyProperties.AUTH_DEVICE_CREDENTIAL)
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").apply { init(builder.build()) }.generateKey()
    }
    private fun prompt(activity: FragmentActivity, cipher: Cipher, title: String, success: (Cipher) -> Unit, failure: (String) -> Unit) {
        val info = BiometricPrompt.PromptInfo.Builder().setTitle(title).setAllowedAuthenticators(authenticators).apply { if (Build.VERSION.SDK_INT < 30) setNegativeButtonText("取消") }.build()
        val prompt = BiometricPrompt(activity, ContextCompat.getMainExecutor(activity), object : BiometricPrompt.AuthenticationCallback() {
            override fun onAuthenticationSucceeded(result: BiometricPrompt.AuthenticationResult) { result.cryptoObject?.cipher?.let(success) ?: failure("无法读取设备加密密钥") }
            override fun onAuthenticationError(code: Int, message: CharSequence) { if (code != BiometricPrompt.ERROR_NEGATIVE_BUTTON && code != BiometricPrompt.ERROR_USER_CANCELED) failure("设备验证未完成，请使用主密码") }
        })
        prompt.authenticate(info, BiometricPrompt.CryptoObject(cipher))
    }
    fun enable(activity: FragmentActivity, vaultKey: VaultKey, done: () -> Unit, failure: (String) -> Unit) {
        try { val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.ENCRYPT_MODE, key()) }
            prompt(activity, cipher, "为墨记启用设备解锁", { c -> try { val material = vaultKey.copyBytes(); val encrypted = try { c.doFinal(material) } finally { material.fill(0) }; val wrapped = WrappedKey(Crypto.b64(c.iv), Crypto.b64(encrypted), vaultKey.salt, vaultKey.iterations); file.parentFile?.mkdirs(); file.writeText(json.encodeToString(wrapped)); done() } catch (_: Exception) { failure("设备密钥保存失败") } }, failure)
        } catch (_: Exception) { failure("当前设备无法启用生物识别解锁") }
    }
    fun unlock(activity: FragmentActivity, success: (VaultKey) -> Unit, failure: (String) -> Unit) {
        try { val wrapped = json.decodeFromString<WrappedKey>(file.readText()); val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, Crypto.unb64(wrapped.iv))) }
            prompt(activity, cipher, "解锁墨记", { c -> try { success(VaultKey(c.doFinal(Crypto.unb64(wrapped.data)), wrapped.salt, wrapped.iterations)) } catch (_: Exception) { disable(); failure("设备密钥已失效，请使用主密码") } }, failure)
        } catch (_: Exception) { disable(); failure("设备密钥已失效，请使用主密码") }
    }
    fun disable() { file.delete(); try { KeyStore.getInstance("AndroidKeyStore").apply { load(null); deleteEntry(ALIAS) } } catch (_: Exception) {} }
    companion object { private const val ALIAS = "moji-vault-biometric-v1" }
}
