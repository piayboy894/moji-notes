package app.moji.android

import app.moji.android.data.*
import app.moji.android.security.*
import app.moji.android.sync.*
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.*
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.*
import org.junit.Assert.*
import org.junit.Test

class CloudSaveTest {
    private fun state() = TestWorkspace.create().copy(sync = SyncSettings(repository = "test-owner/xuxian-data", token = "fake-private-token", code = ""))
    @Test fun loadingReplacesAllProjectsWithoutMergingAndKeepsDevicePreferences() {
        val local = state().copy(preferences = Preferences(theme = "dark", autoSync = true)); val source = TestWorkspace.create()
        val loaded = GameSaves.apply(local, GameSaves.capture(source)); assertEquals(source.notebooks.map { it.id }, loaded.notebooks.map { it.id }); assertEquals(source.notes.map { it.id }, loaded.notes.map { it.id }); assertTrue(loaded.notes.none { n -> local.notes.any { it.id == n.id } }); assertEquals(local.sync, loaded.sync); assertEquals("dark", loaded.preferences.theme); assertFalse(loaded.preferences.autoSync)
    }
    @Test fun fullSaveIncludesTrashArchiveAndHistoryAcrossAllModes() {
        val original = state(); val source = original.changeNote(original.notes[0].id, true) { it.copy(content = "save-data", archived = true, deletedAt = now()) }
        val payload = GameSaves.capture(source); val restored = GameSaves.apply(Workspace(), payload)
        assertEquals(source.notes.size, restored.notes.size); assertNotNull(restored.notes[0].deletedAt); assertTrue(restored.notes[0].archived); assertTrue(restored.notes[0].history.isNotEmpty()); assertEquals(3, restored.notes.map { it.notebookId }.distinct().size)
        val text = json.encodeToString(payload); assertFalse(text.contains("fake-private-token")); assertFalse(text.contains("preferences"))
    }
    @Test fun invalidLoadDoesNotProduceAPartialWorkspace() { val local = state(); assertThrows(IllegalArgumentException::class.java) { GameSaves.apply(local, SyncPayload(1, "bad", emptyList(), local.notes)) }; assertEquals(3, local.notes.size) }
    @Test fun windowsGeneratedGlobalCatalogAndFullDataDecryptOnAndroid() {
        val fixture = json.parseToJsonElement(javaClass.getResource("/windows-save-catalog-v1.json")!!.readText()).jsonObject; val password = fixture.getValue("password").jsonPrimitive.content
        fun envelope(field: String) = json.decodeFromJsonElement<Envelope>(fixture.getValue(field))
        val indexEnvelope = envelope("indexEnvelope"); val dataEnvelope = envelope("dataEnvelope")
        Crypto.derive(password, indexEnvelope.salt, indexEnvelope.iterations).use { key -> val index = Crypto.decrypt<SaveIndex>(indexEnvelope, key); GameSaves.validate(index); assertEquals(fixture.getValue("expectedName").jsonPrimitive.content, index.entries.single().name) }
        Crypto.derive(password, dataEnvelope.salt, dataEnvelope.iterations).use { key -> val data = Crypto.decrypt<SyncPayload>(dataEnvelope, key); val local = state(); val loaded = GameSaves.apply(local, data); assertEquals(fixture.getValue("expectedContent").jsonPrimitive.content, loaded.notes[0].content); assertEquals(fixture.getValue("expectedNotes").jsonPrimitive.int, loaded.notes.size); assertTrue(loaded.notes[0].history.isNotEmpty()); assertEquals(local.sync, loaded.sync) }
    }
    @Test fun manualCreateOverwriteReadDeleteAreAtomicAndPreserveOtherSoftware() = runBlocking {
        MockWebServer().use { server -> val git = MemoryGit(); server.dispatcher = git; server.start(); val service = CloudSaveService(OkHttpClient(), server.url("/").toString().trimEnd('/')); val state = state()
            var catalog = service.list(state.sync); assertTrue(catalog.entries.isEmpty()); assertEquals(0, git.writes)
            catalog = service.write(state.sync, catalog, state, "手机存档"); val entry = catalog.entries.single()
            val changed = state.changeNote(state.notes[0].id, true) { it.copy(content = "replacement") }; catalog = service.write(state.sync, catalog, changed, "覆盖名称", entry)
            val payload = service.read(state.sync, catalog, catalog.entries.single()); assertEquals("replacement", payload.notes[0].content); assertTrue(payload.notes[0].history.isNotEmpty())
            catalog = service.write(state.sync, catalog, state, "第二份"); assertEquals(2, catalog.entries.size); catalog = service.list(state.sync); val removed = catalog.entries.first { it.name == "覆盖名称" }
            catalog = service.delete(state.sync, catalog, removed); assertEquals(1, catalog.entries.size); assertFalse(git.files.containsKey(CloudSaveService.root() + removed.id + ".json")); assertEquals("OTHER_APP_DATA", git.text("other-app/data.json"))
            assertTrue(git.changedPaths.all { it.startsWith(CloudSaveService.root()) }); assertTrue(git.uploads.all { !it.contains("fake-private-token") }); assertFalse(git.forced)
        }
    }
    @Test fun staleSaveNeverOverwritesOrRetriesAutomatically() = runBlocking {
        MockWebServer().use { server -> val git = MemoryGit(); server.dispatcher = git; server.start(); val service = CloudSaveService(OkHttpClient(), server.url("/").toString().trimEnd('/')); val state = state(); val old = service.list(state.sync)
            service.write(state.sync, old, state, "first"); val head = git.head; val writes = git.refWrites
            val error = runCatching { service.write(state.sync, old, state, "stale") }.exceptionOrNull(); assertTrue(error!!.message!!.contains("刷新")); assertEquals(head, git.head); assertEquals(writes + 1, git.refWrites); assertFalse(git.forced); assertEquals(1, service.list(state.sync).entries.size)
        }
    }
    @Test fun legacySaveIsOnlyReadOrDeletedWhenSelected() = runBlocking {
        MockWebServer().use { server -> val git = MemoryGit(); val state = state().let{it.copy(sync=it.sync.copy(code="ManualSaveCode2026"))}; Crypto.newKey(state.sync.code).use { git.add(CloudSaveService.legacyPath(state.sync.code), json.encodeToString(Crypto.encrypt(state.payload(), it))) }; server.dispatcher = git; server.start(); val service = CloudSaveService(OkHttpClient(), server.url("/").toString().trimEnd('/'))
            val catalog = service.list(state.sync); val entry = catalog.entries.single(); assertTrue(entry.legacy); assertEquals(0, git.writes); assertEquals(state.notes.map { it.id }, service.read(state.sync, catalog, entry).notes.map { it.id }); assertTrue(service.delete(state.sync, catalog, entry).entries.isEmpty()); assertFalse(git.files.containsKey(CloudSaveService.legacyPath(state.sync.code)))
        }
    }
    @Test fun emptyRepositoryCanBeInitializedAndConnectionChangesCannotUseOldCatalog() = runBlocking {
        MockWebServer().use { server -> val git = MemoryGit(true); server.dispatcher = git; server.start(); val service = CloudSaveService(OkHttpClient(), server.url("/").toString().trimEnd('/')); val state = state(); val empty = service.list(state.sync); assertNull(empty.headSha)
            val catalog = service.write(state.sync, empty, state, "first"); assertEquals(1, service.list(state.sync).entries.size); assertTrue(git.files.keys.all { it.startsWith(CloudSaveService.root()) }); val writes = git.writes
            assertNotNull(runCatching { service.write(state.sync.copy(repository = "another/repository"), catalog, state, "wrong") }.exceptionOrNull()); assertEquals(writes, git.writes)
        }
    }
    @Test fun corruptCatalogCannotAddressOtherPaths() { assertThrows(IllegalArgumentException::class.java) { GameSaves.validate(SaveIndex("moji-saves", 1, listOf(SaveEntry("../../other-app/data.json", "bad", 1, 0, 0, "a".repeat(40), "test")))) } }

    @Test fun anotherDeviceNeedsOnlyRepositoryAndToken() = runBlocking {
        MockWebServer().use { server ->val git=MemoryGit();server.dispatcher=git;server.start();val service=CloudSaveService(OkHttpClient(),server.url("/").toString().trimEnd('/'));val w=state();val empty=service.list(w.sync);service.write(w.sync,empty,w,"code-free");val c=SyncSettings(repository=w.sync.repository,token="second-device-token");val remote=service.list(c);assertEquals(1,remote.entries.size);assertEquals(w.notes,service.read(c,remote,remote.entries.single()).notes);assertTrue(git.files.containsKey("moji-notes/saves/index.json"))}
    }
    @Test fun previousCatalogIsReadOnlyUntilExplicitlyConvertedOrDeleted() = runBlocking {
        MockWebServer().use {server->val git=MemoryGit();val w=state().let{it.copy(sync=it.sync.copy(code="ManualSaveCode2026"))};val entries=(0..1).map{i->val id=uid();val path=CloudSaveService.oldRoot(w.sync.code)+id+".moji";Crypto.newKey(w.sync.code).use{git.add(path,json.encodeToString(Crypto.encrypt(w.payload(),it)))};SaveEntry(id,"old$i",now(),3,3,git.files.getValue(path),"Android")}
            Crypto.newKey(w.sync.code).use{git.add(CloudSaveService.oldRoot(w.sync.code)+"index.moji",json.encodeToString(Crypto.encrypt(SaveIndex("moji-saves",1,entries),it)))}
            server.dispatcher=git;server.start();val service=CloudSaveService(OkHttpClient(),server.url("/").toString().trimEnd('/'));var c=service.list(w.sync);assertEquals(2,c.entries.size);assertTrue(c.entries.all{it.legacy});assertEquals(0,git.writes);assertEquals(w.id,service.read(w.sync,c,c.entries.first()).id)
            c=service.write(w.sync,c,w,"converted",c.entries.first());c=service.list(w.sync);assertEquals(2,c.entries.size);assertEquals(1,c.entries.count{it.legacy});c=service.delete(w.sync,c,c.entries.first{it.legacy});assertEquals(1,service.list(w.sync).entries.size);assertEquals("OTHER_APP_DATA",git.text("other-app/data.json"))
        }
    }
    private class MemoryGit(empty: Boolean = false) : Dispatcher() {
        private var next = 10; private fun id() = (next++).toString(16).padStart(40, '0')
        private val blobs = mutableMapOf<String, String>(); private val trees = mutableMapOf<String, MutableMap<String, String>>(); private val commits = mutableMapOf<String, Pair<String, String?>>()
        var head: String? = null; private set
        val files get() = head?.let { trees.getValue(commits.getValue(it).first) } ?: mutableMapOf()
        var writes = 0; private set; var refWrites = 0; private set; var forced = false; private set
        val changedPaths = mutableListOf<String>(); val uploads = mutableListOf<String>()
        init { if (!empty) add("other-app/data.json", "OTHER_APP_DATA") }
        fun add(path: String, text: String) { if (head == null) { head = id(); val tree = id(); trees[tree] = mutableMapOf(); commits[head!!] = tree to null }; val sha = id(); blobs[sha] = text; files[path] = sha }
        fun text(path: String) = blobs[files[path]]
        private fun reply(value: JsonObject = buildJsonObject {}, status: Int = 200) = MockResponse().setResponseCode(status).setBody(value.toString())
        override fun dispatch(request: RecordedRequest): MockResponse {
            val path = request.requestUrl!!.encodedPath.substringAfter("/xuxian-data"); val method = request.method!!
            if (method == "GET") {
                if (path.isEmpty()) return reply(buildJsonObject { put("default_branch", "main") })
                if (path.startsWith("/git/ref/heads/")) return if (head == null) reply(status = 404) else reply(buildJsonObject { put("object", buildJsonObject { put("sha", head) }) })
                if (path.startsWith("/git/commits/")) return reply(buildJsonObject { put("tree", buildJsonObject { put("sha", commits.getValue(path.substringAfterLast('/')).first) }) })
                if (path.startsWith("/git/blobs/")) { val text = blobs.getValue(path.substringAfterLast('/')); return reply(buildJsonObject { put("size", text.toByteArray().size); put("content", Crypto.b64(text.toByteArray())) }) }
                if (path.startsWith("/contents/")) { val name = path.removePrefix("/contents/"); val h = request.requestUrl!!.queryParameter("ref") ?: head; val t = h?.let { trees.getValue(commits.getValue(it).first) } ?: emptyMap(); val sha = t[name] ?: return reply(status = 404); val text = blobs.getValue(sha); return reply(buildJsonObject { put("sha", sha); put("size", text.toByteArray().size); put("content", Crypto.b64(text.toByteArray())) }) }
            }
            writes++; val body = json.parseToJsonElement(request.body.readUtf8()).jsonObject
            if (path == "/git/blobs") { val sha = id(); val text = Crypto.unb64(body.getValue("content").jsonPrimitive.content).toString(Charsets.UTF_8); blobs[sha] = text; uploads += text; return reply(buildJsonObject { put("sha", sha) }, 201) }
            if (path == "/git/trees") { val tree = body["base_tree"]?.jsonPrimitive?.content?.let { trees.getValue(it).toMutableMap() } ?: mutableMapOf(); body.getValue("tree").jsonArray.forEach { value -> val item = value.jsonObject; val p = item.getValue("path").jsonPrimitive.content; changedPaths += p; val sha = item["sha"]?.jsonPrimitive?.contentOrNull; if (sha == null) tree.remove(p) else tree[p] = sha }; val sha = id(); trees[sha] = tree; return reply(buildJsonObject { put("sha", sha) }, 201) }
            if (path == "/git/commits") { val sha = id(); commits[sha] = body.getValue("tree").jsonPrimitive.content to body.getValue("parents").jsonArray.firstOrNull()?.jsonPrimitive?.content; return reply(buildJsonObject { put("sha", sha) }, 201) }
            if (path.startsWith("/git/refs/heads/")) { refWrites++; forced = forced || body.getValue("force").jsonPrimitive.boolean; val sha = body.getValue("sha").jsonPrimitive.content; if (commits.getValue(sha).second != head) return reply(status = 409); head = sha; return reply() }
            if (path.startsWith("/contents/") && method == "PUT") { val p = path.removePrefix("/contents/"); if (files.containsKey(p)) return reply(status = 422); add(p, Crypto.unb64(body.getValue("content").jsonPrimitive.content).toString(Charsets.UTF_8)); return reply(status = 201) }
            return reply(status = 500)
        }
    }
}
