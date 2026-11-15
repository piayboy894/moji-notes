package app.moji.android

import app.moji.android.data.*
import app.moji.android.security.*
import app.moji.android.sync.*
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.Serializable
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.*
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.*
import org.junit.Test
import java.io.File

class CoreTest {
    @Test fun aesRoundTripUsesFreshNoncesAndRejectsWrongPasswords() {
        Crypto.newKey("Master-Password-2026").use { key ->
            val state = TestWorkspace.create(); val first = Crypto.encrypt(state, key); val second = Crypto.encrypt(state, key)
            assertNotEquals(first.iv, second.iv); assertNotEquals(first.data, second.data)
            assertFalse(json.encodeToString(first).contains(state.notes[0].title))
            assertEquals(state, Crypto.decrypt<Workspace>(first, key))
            Crypto.derive("wrong-password", key.salt).use { wrong -> assertThrows(IllegalArgumentException::class.java) { Crypto.decrypt<Workspace>(first, wrong) } }
        }
    }
    @Test fun tamperingFailsAuthentication() { Crypto.newKey("pw").use { key -> val e = Crypto.encrypt(TestWorkspace.create(), key); val bytes = Crypto.unb64(e.data); bytes[0] = (bytes[0].toInt() xor 1).toByte(); assertThrows(IllegalArgumentException::class.java) { Crypto.decrypt<Workspace>(e.copy(data = Crypto.b64(bytes)), key) } } }
    @Test fun rejectsUnboundedKdfParameters() { assertThrows(IllegalArgumentException::class.java) { Crypto.derive("pw", Crypto.b64(ByteArray(16)), Int.MAX_VALUE) } }
    @Test fun generatedPasswordsMeetComposition() { repeat(60) { val p = Crypto.password(32); assertEquals(32, p.length); assertTrue(p.any(Char::isUpperCase)); assertTrue(p.any(Char::isLowerCase)); assertTrue(p.any(Char::isDigit)); assertTrue(p.any { !it.isLetterOrDigit() }); assertTrue(Crypto.password(24, false).all(Char::isLetterOrDigit)) } }
    @Test fun windowsGeneratedUnicodeEnvelopeDecryptsOnAndroidJvm() {
        val text = javaClass.getResource("/windows-v1-vector.json")!!.readText(); val objectValue = json.parseToJsonElement(text).jsonObject
        val password = objectValue.getValue("password").jsonPrimitive.content; val envelope = json.decodeFromJsonElement<Envelope>(objectValue.getValue("envelope"))
        Crypto.derive(password, envelope.salt, envelope.iterations).use { key -> val payload = Crypto.decrypt<SyncPayload>(envelope, key); assertEquals(objectValue.getValue("expectedContent").jsonPrimitive.content, payload.notes[0].content); assertEquals(objectValue.getValue("expectedPassword").jsonPrimitive.content, payload.notes[0].password); assertEquals(NoteMode.Prompt, payload.notebooks[1].mode); assertTrue(payload.notes[0].history.isNotEmpty()) }
    }
    @Test fun androidEnvelopeSurvivesDesktopCompatibleRoundTrip() { val w = TestWorkspace.create(); val key = Crypto.newKey("中文密码-Pass123"); key.use { val e = Crypto.encrypt(w.payload(), key); Crypto.derive("中文密码-Pass123", e.salt, e.iterations).use { k -> val remote = Crypto.decrypt<SyncPayload>(e, k); assertEquals(w.notebooks, remote.notebooks); assertEquals(w.notes, remote.notes); assertFalse(Crypto.decryptText(e, k).contains("token")) } } }
    @Test fun historyIsBounded() { var n = Note(content = "0"); repeat(50) { i -> n = n.edited(n.copy(content = i.toString()), true) }; assertEquals(40, n.history.size); assertEquals("48", n.history.first().content) }
    @Test fun totpMatchesRfc6238() { assertEquals("287082", Totp.code("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", 59000)) }
    @Test fun passwordSearchDoesNotExposeSensitiveFields() { val n = Note(username = "sample@example.com", password = "super-secret-password", otp = "secret-otp", content = "private-memo"); assertTrue(Records.matches(n, "sample", NoteMode.Password)); assertFalse(Records.matches(n, "super-secret", NoteMode.Password)); assertFalse(Records.matches(n, "private-memo", NoteMode.Password)) }
    @Test fun validationRejectsOrphanedNotes() { val w = TestWorkspace.create(); assertThrows(IllegalArgumentException::class.java) { w.copy(notes = listOf(w.notes[0].copy(notebookId = "absent"))).validate() } }
    @Test fun atomicPlainStoreRecoversBackup() = runBlocking {
        val directory = java.nio.file.Files.createTempDirectory("moji-jvm").toFile(); val store = VaultStore(directory)
        try { Crypto.newKey("password").use { key -> val w = TestWorkspace.create(); store.save(w); store.save(w.changeNote(w.notes[0].id) { it.copy(content = "private-value") }); assertTrue(store.file.readText().contains("private-value")); assertTrue(File(directory, "workspace.json.bak").exists()); assertEquals("private-value", store.read()!!.notes[0].content); store.file.writeText("damaged"); assertNotNull(store.read()); assertTrue(store.recoveredBackup) } } finally { directory.deleteRecursively() }
    }
    @Test fun androidUpdateTagsRemainSeparateFromWindows() { assertEquals(10002, AndroidUpdater.versionCode("1.0.2")); assertNull(AndroidUpdater.versionCode("v1.0.2")); assertNull(AndroidUpdater.versionCode("1.0.2-beta")); assertEquals(CloudSaveService.legacyPath("TestSyncCode2026"), "moji-notes/${Crypto.hash("TestSyncCode2026").take(40)}.moji") }
}
