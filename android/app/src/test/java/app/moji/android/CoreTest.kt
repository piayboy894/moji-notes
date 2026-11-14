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
            val state = Seed.create(); val first = Crypto.encrypt(state, key); val second = Crypto.encrypt(state, key)
            assertNotEquals(first.iv, second.iv); assertNotEquals(first.data, second.data)
            assertFalse(json.encodeToString(first).contains(state.notes[0].title))
            assertEquals(state, Crypto.decrypt<Workspace>(first, key))
            Crypto.derive("wrong-password", key.salt).use { wrong -> assertThrows(IllegalArgumentException::class.java) { Crypto.decrypt<Workspace>(first, wrong) } }
        }
    }
    @Test fun tamperingFailsAuthentication() { Crypto.newKey("pw").use { key -> val e = Crypto.encrypt(Seed.create(), key); val bytes = Crypto.unb64(e.data); bytes[0] = (bytes[0].toInt() xor 1).toByte(); assertThrows(IllegalArgumentException::class.java) { Crypto.decrypt<Workspace>(e.copy(data = Crypto.b64(bytes)), key) } } }
    @Test fun rejectsUnboundedKdfParameters() { assertThrows(IllegalArgumentException::class.java) { Crypto.derive("pw", Crypto.b64(ByteArray(16)), Int.MAX_VALUE) } }
    @Test fun generatedPasswordsMeetComposition() { repeat(60) { val p = Crypto.password(32); assertEquals(32, p.length); assertTrue(p.any(Char::isUpperCase)); assertTrue(p.any(Char::isLowerCase)); assertTrue(p.any(Char::isDigit)); assertTrue(p.any { !it.isLetterOrDigit() }); assertTrue(Crypto.password(24, false).all(Char::isLetterOrDigit)) } }
    @Test fun windowsGeneratedUnicodeEnvelopeDecryptsOnAndroidJvm() {
        val text = javaClass.getResource("/windows-v1-vector.json")!!.readText(); val objectValue = json.parseToJsonElement(text).jsonObject
        val password = objectValue.getValue("password").jsonPrimitive.content; val envelope = json.decodeFromJsonElement<Envelope>(objectValue.getValue("envelope"))
        Crypto.derive(password, envelope.salt, envelope.iterations).use { key -> val payload = Crypto.decrypt<SyncPayload>(envelope, key); assertEquals(objectValue.getValue("expectedContent").jsonPrimitive.content, payload.notes[0].content); assertEquals(objectValue.getValue("expectedPassword").jsonPrimitive.content, payload.notes[0].password); assertEquals(NoteMode.Prompt, payload.notebooks[1].mode); assertTrue(payload.notes[0].history.isNotEmpty()) }
    }
    @Test fun androidEnvelopeSurvivesDesktopCompatibleRoundTrip() { val w = Seed.create(); val key = Crypto.newKey("中文密码-Pass123"); key.use { val e = Crypto.encrypt(w.payload(), key); Crypto.derive("中文密码-Pass123", e.salt, e.iterations).use { k -> val remote = Crypto.decrypt<SyncPayload>(e, k); assertEquals(w.notebooks, remote.notebooks); assertEquals(w.notes, remote.notes); assertFalse(Crypto.decryptText(e, k).contains("token")) } } }
    @Test fun singleEndedEditUpdatesOriginal() { val original = Seed.create(); val remote = original.changeNote(original.notes[0].id) { it.copy(content = "remote") }; val merged = WorkspaceMerge.merge(original, remote.payload(), WorkspaceMerge.revisions(original)); assertEquals(original.notes.size, merged.notes.size); assertEquals("remote", merged.notes.first().content); assertEquals(0, merged.conflicts) }
    @Test fun concurrentEditsPreserveBothVersions() { val original = Seed.create(); val left = original.changeNote(original.notes[0].id) { it.copy(content = "local") }; val right = original.changeNote(original.notes[0].id) { it.copy(content = "remote") }; val merged = WorkspaceMerge.merge(left, right.payload(), WorkspaceMerge.revisions(original)); assertEquals(1, merged.conflicts); assertTrue(merged.notes.any { it.content == "local" }); assertTrue(merged.notes.any { it.content == "remote" && it.title.endsWith("同步副本") }) }
    @Test fun deletionCannotBeResurrectedByOldDevice() { val original = Seed.create(); val left = original.changeNote(original.notes[0].id) { it.copy(deletedAt = now()) }; val result = WorkspaceMerge.merge(left, original.payload(), WorkspaceMerge.revisions(original)); assertNotNull(result.notes[0].deletedAt); assertEquals(0, result.conflicts) }
    @Test fun editAndDeleteConflictPreservesEditedCopy() { val original = Seed.create(); val left = original.changeNote(original.notes[0].id) { it.copy(deletedAt = now()) }; val right = original.changeNote(original.notes[0].id) { it.copy(content = "valuable") }; val result = WorkspaceMerge.merge(left, right.payload(), WorkspaceMerge.revisions(original)); assertNotNull(result.notes[0].deletedAt); assertTrue(result.notes.any { it.content == "valuable" && it.deletedAt == null }) }
    @Test fun deletedNotebooksMoveAssociatedNotesToTrash() { val original = Seed.create(); val right = original.copy(notebooks = original.notebooks.mapIndexed { i, b -> if (i == 0) b.copy(deletedAt = now(), updatedAt = b.updatedAt + 100, revision = uid()) else b }); val result = WorkspaceMerge.merge(original, right.payload(), WorkspaceMerge.revisions(original)); assertNotNull(result.notes[0].deletedAt) }
    @Test fun historyIsBounded() { var n = Note(content = "0"); repeat(50) { i -> n = n.edited(n.copy(content = i.toString()), true) }; assertEquals(40, n.history.size); assertEquals("48", n.history.first().content) }
    @Test fun variablesAreUniqueAndReplacementIsNotRecursive() { assertEquals(listOf("主题", "平台"), Records.variables("{{主题}} {{ 主题 }} {{平台}}")); assertEquals("{{平台}} 博客", Records.fill("{{主题}} {{平台}}", mapOf("主题" to "{{平台}}", "平台" to "博客"))) }
    @Test fun totpMatchesRfc6238() { assertEquals("287082", Totp.code("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", 59000)) }
    @Test fun passwordSearchDoesNotExposeSensitiveFields() { val n = Note(username = "sample@example.com", password = "super-secret-password", otp = "secret-otp", content = "private-memo"); assertTrue(Records.matches(n, "sample", NoteMode.Password)); assertFalse(Records.matches(n, "super-secret", NoteMode.Password)); assertFalse(Records.matches(n, "private-memo", NoteMode.Password)) }
    @Test fun validationRejectsOrphanedNotes() { val w = Seed.create(); assertThrows(IllegalArgumentException::class.java) { w.copy(notes = listOf(w.notes[0].copy(notebookId = "absent"))).validate() } }
    @Test fun atomicStoreRecoversAnEncryptedBackup() = runBlocking {
        val directory = java.nio.file.Files.createTempDirectory("moji-jvm").toFile(); val store = VaultStore(directory)
        try { Crypto.newKey("password").use { key -> val w = Seed.create(); store.save(w, key); store.save(w.changeNote(w.notes[0].id) { it.copy(content = "private-value") }, key); assertFalse(store.file.readText().contains("private-value")); assertTrue(File(directory, "vault.moji.bak").exists()); assertEquals("private-value", Crypto.decrypt<Workspace>(store.read()!!, key).notes[0].content); store.file.writeText("damaged"); assertNotNull(store.read()); assertTrue(store.recoveredBackup) } } finally { directory.deleteRecursively() }
    }
    @Test fun androidUpdateTagsRemainSeparateFromWindows() { assertEquals(10002, AndroidUpdater.versionCode("1.0.2")); assertNull(AndroidUpdater.versionCode("v1.0.2")); assertNull(AndroidUpdater.versionCode("1.0.2-beta")); assertEquals(GitHubSync.dataPath("TestSyncCode2026"), "moji-notes/${Crypto.hash("TestSyncCode2026").take(40)}.moji") }
    @Test fun repositorySyncUploadsOnlyEncryptedPayloadAndUsesDedicatedDirectory() = runBlocking {
        MockWebServer().use { server -> server.start(); server.enqueue(MockResponse().setResponseCode(404)); server.enqueue(MockResponse().setBody("{}")); server.enqueue(MockResponse().setResponseCode(201).setBody("{}"))
            val w = Seed.create().copy(sync = SyncSettings(token = "fake-test-token", code = "TestSyncCode2026")); val result = GitHubSync(OkHttpClient(), server.url("/").toString().trimEnd('/')).synchronize(w)
            server.takeRequest(); server.takeRequest(); val write = server.takeRequest(); assertTrue(write.path!!.contains("/contents/moji-notes/")); val body = json.parseToJsonElement(write.body.readUtf8()).jsonObject; val envelope = json.decodeFromString<Envelope>(Crypto.unb64(body.getValue("content").jsonPrimitive.content).toString(Charsets.UTF_8)); Crypto.derive(w.sync.code, envelope.salt, envelope.iterations).use { key -> val payload = json.parseToJsonElement(Crypto.decryptText(envelope, key)).jsonObject; assertFalse(payload.containsKey("sync")); assertFalse(payload.containsKey("preferences")) }; assertTrue(result.first.sync.lastSync > 0)
        }
    }
    @Test fun concurrentGithubWritesStopWithShaPrecondition() = runBlocking {
        MockWebServer().use { server -> server.start(); val w = Seed.create().copy(sync = SyncSettings(token = "fake", code = "TestSyncCode2026")); val encrypted = Crypto.newKey(w.sync.code).use { Crypto.encrypt(w.payload(), it) }; server.enqueue(MockResponse().setBody("""{"sha":"original-sha","content":"${Crypto.b64(json.encodeToString(encrypted).toByteArray())}","size":1000}""")); server.enqueue(MockResponse().setResponseCode(409).setBody("{}")); val error = runCatching { GitHubSync(OkHttpClient(), server.url("/").toString().trimEnd('/')).synchronize(w) }.exceptionOrNull(); assertTrue(error!!.message!!.contains("重新同步")); server.takeRequest(); assertTrue(server.takeRequest().body.readUtf8().contains("original-sha")) }
    }
}
