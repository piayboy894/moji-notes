package app.moji.android

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import app.moji.android.data.*
import app.moji.android.security.*
import app.moji.android.sync.*
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.serialization.encodeToString
import java.io.File

data class TextImport(val name:String,val text:String,val markdown:Boolean=false)

data class AppState(
    val workspace: Workspace? = null, val loading: Boolean = true, val exists: Boolean = false,
    val busy: Boolean = false, val error: String = "", val saving: Boolean = false,
    val saveError: Boolean = false, val view: String = "home", val selectedId: String? = null,
    val editingCredentialId: String? = null, val search: String = "", val sort: String = "updated", val update: AndroidUpdate? = null,
    val updateChecking: Boolean = false, val updateProgress: Float? = null, val downloadedApk: File? = null,
    val needsMigration: Boolean = false, val textImport:TextImport? = null
)
class MojiViewModel(application: Application) : AndroidViewModel(application) {
    val store = VaultStore(File(application.noBackupFilesDir, "vault"))
    val updater = AndroidUpdater(application)
    private val mutable = MutableStateFlow(AppState())
    val state = mutable.asStateFlow()
    private val notices = MutableSharedFlow<String>(extraBufferCapacity = 10)
    val messages = notices.asSharedFlow()
    private var saveJob: Job? = null
    private val saveMutex = Mutex(); private var serial = 0L; private var savedSerial = 0L
    private var epoch = 0L; private var lastUpdateCheck = 0L
    private var loadingSave = false
    val cursors = mutableMapOf<String, Pair<Int, Int>>()
    val undo = mutableMapOf<String, ArrayDeque<String>>()
    val redo = mutableMapOf<String, ArrayDeque<String>>()
    private var pendingImport: TextImport? = null
    init { openLocal() }
    fun message(text: String) { notices.tryEmit(text) }
    fun touch() { }
    fun openLocal() = viewModelScope.launch {
        mutable.update { it.copy(loading = true, error = "") }
        try {
            val stored = store.read()
            if (stored == null && store.hasLegacy) { mutable.update { it.copy(loading = false, exists = true, needsMigration = true) }; return@launch }
            val workspace = stored ?: Seed.create().also { store.save(it) }
            open(workspace); if (store.recoveredBackup) message("已恢复上一份本地备份")
        } catch (error: Exception) { mutable.update { it.copy(loading = false, error = UserMessages.forError(error,"本地数据无法读取")) } }
    }
    fun migrate(password: String) = viewModelScope.launch {
        if (mutable.value.busy) return@launch
        mutable.update { it.copy(busy = true, error = "") }
        try {
            val envelope = store.readLegacy()
            val workspace = withContext(Dispatchers.Default) { Crypto.derive(password, envelope.salt, envelope.iterations).use { Crypto.decrypt<Workspace>(envelope, it).validate() } }
            store.save(workspace); open(workspace)
        } catch (error: Exception) { mutable.update { it.copy(busy = false, error = UserMessages.forError(error,"迁移失败")) } }
    }
    private fun open(workspace: Workspace) {
        epoch++; serial = 0; savedSerial = 0
        mutable.value = AppState(workspace = workspace, loading = false, exists = true)
        pendingImport?.let { pendingImport = null; importText(it.name,it.text,markdown=it.markdown) }
        if (now() - lastUpdateCheck > 6 * 3_600_000) { lastUpdateCheck = now(); checkUpdate(false) }
    }
    fun mutate(change: (Workspace) -> Workspace) {
        val s = mutable.value; val w = s.workspace ?: return
        if (loadingSave) return
        serial++; mutable.value = s.copy(workspace = change(w), saving = true, saveError = false); touch()
        saveJob?.cancel(); saveJob = viewModelScope.launch { delay(350); try { flush() } catch (error: Exception) { mutable.update { it.copy(saveError = true, saving = false) }; message("保存失败，请先导出存档") } }
    }
    fun edit(id: String, checkpoint: Boolean = false, change: (Note) -> Note) = mutate { it.changeNote(id, checkpoint, change) }
    fun content(id: String, text: String, history: Boolean = true) {
        val current = mutable.value.workspace?.notes?.firstOrNull { it.id == id } ?: return
        if (current.content == text) return
        if (history) { val stack = undo.getOrPut(id) { ArrayDeque() }; if (stack.lastOrNull() != current.content) { stack.addLast(current.content); while (stack.size > 60) stack.removeFirst() }; redo.remove(id) }
        edit(id) { it.copy(content = text) }
    }
    fun undo(id: String) { val before = undo[id]?.removeLastOrNull() ?: return; val current = selectedNote()?.content ?: ""; redo.getOrPut(id) { ArrayDeque() }.addLast(current); content(id, before, false) }
    fun redo(id: String) { val after = redo[id]?.removeLastOrNull() ?: return; undo.getOrPut(id) { ArrayDeque() }.addLast(selectedNote()?.content ?: ""); content(id, after, false) }
    suspend fun flush() = saveMutex.withLock {
        val s = mutable.value; val w = s.workspace ?: return@withLock
        if (serial == savedSerial) return@withLock
        val captured = serial; store.save(w); savedSerial = captured; mutable.update { it.copy(saving = serial != savedSerial, saveError = false) }
    }
    fun background() { viewModelScope.launch { try { flush() } catch (_: Exception) { message("保存失败，请导出备份") } } }
    fun foreground() { }
    fun navigate(view: String) { mutable.update { it.copy(view = view, selectedId = null, editingCredentialId = null, search = "") }; touch() }
    fun select(id: String) { mutable.update { it.copy(selectedId = id, editingCredentialId = if (it.selectedId == id) it.editingCredentialId else null) }; touch() }
    fun clearSelection() { mutable.update { it.copy(selectedId = null, editingCredentialId = null) } }
    fun search(value: String) { mutable.update { it.copy(search = value) }; touch() }
    fun sort(value: String) { mutable.update { it.copy(sort = value) } }
    fun editCredential(enabled: Boolean) { mutable.update { it.copy(editingCredentialId = if (enabled) it.selectedId else null) } }
    fun selectedNote() = mutable.value.workspace?.notes?.firstOrNull { it.id == mutable.value.selectedId }
    fun filtered(): List<Note> {
        val s = mutable.value; val w = s.workspace ?: return emptyList(); val books = w.notebooks.associateBy { it.id }
        val notes = w.notes.filter { n -> n.purgedAt == null && (if (s.view == "trash") n.deletedAt != null else n.deletedAt == null && books[n.notebookId]?.deletedAt == null && (if (s.view == "archive") n.archived else !n.archived)) && (s.view != "star" || n.favorite) && (w.liveBooks.none { it.id == s.view } || n.notebookId == s.view) && Records.matches(n, s.search, books[n.notebookId]?.mode ?: NoteMode.Text) }
        return when (s.sort) { "name" -> notes.sortedWith(compareByDescending<Note> { it.pinned }.thenBy { it.title }); "created" -> notes.sortedWith(compareByDescending<Note> { it.pinned }.thenByDescending { it.createdAt }); else -> notes.sortedWith(compareByDescending<Note> { it.pinned }.thenByDescending { it.updatedAt }) }
    }
    fun addNote(notebookId: String = mutable.value.view) {
        val w = mutable.value.workspace ?: return
        val note = NoteCreation.inNotebook(w, notebookId)
        mutate { it.copy(notes = listOf(note) + it.notes) }
        mutable.update { it.copy(view = note.notebookId, selectedId = note.id, editingCredentialId = if (w.liveBooks.first { b -> b.id == notebookId }.mode == NoteMode.Password) note.id else null, search = "") }
    }
    fun saveBook(existing: Notebook?, name: String, mode: NoteMode, color: String, icon:String="") { if (name.isBlank()) return; if (existing == null) { val b = Notebook(name = name.trim(), mode = mode, color = color, icon=icon); mutate { it.copy(notebooks = it.notebooks + b) }; navigate(b.id) } else mutate { it.copy(notebooks = it.notebooks.map { b -> if (b.id == existing.id) b.copy(name = name.trim(), color = color, icon=icon, revision = uid(), updatedAt = now()) else b }) } }
    fun deleteBook(book: Notebook) { mutate { it.copy(notebooks = it.notebooks.map { b -> if (b.id == book.id) b.copy(deletedAt = now(), updatedAt = now(), revision = uid()) else b }, notes = it.notes.map { n -> if (n.notebookId == book.id && n.deletedAt == null) n.edited(n.copy(deletedAt = now())) else n }) }; navigate("home") }
    fun trash(ids: Set<String>) { mutate { it.copy(notes = it.notes.map { n -> if (n.id in ids) n.edited(n.copy(deletedAt = now())) else n }) }; mutable.update { it.copy(selectedId = null, editingCredentialId = null) }; message("已移到回收站") }
    fun restore(ids: Set<String>) { mutate { w -> val books = w.notes.filter { it.id in ids }.map { it.notebookId }.toSet(); w.copy(notebooks = w.notebooks.map { b -> if (b.id in books && b.deletedAt != null) b.copy(deletedAt = null, updatedAt = now(), revision = uid()) else b }, notes = w.notes.map { n -> if (n.id in ids) n.edited(n.copy(deletedAt = null)) else n }) }; mutable.update { it.copy(selectedId = null, editingCredentialId = null) }; message("记录已恢复") }
    fun purge(ids: Set<String>) { mutate { it.copy(notes = it.notes.map { n -> if (n.id in ids) n.copy(title = "", content = "", username = "", password = "", url = "", otp = "", tags = emptyList(), variables = emptyList(), history = emptyList(), deletedAt = n.deletedAt ?: now(), purgedAt = now(), revision = uid(), updatedAt = now()) else n }) }; mutable.update { it.copy(selectedId = null, editingCredentialId = null) } }
    fun duplicate(note: Note) { val copy = note.copy(id = uid(), title = "${note.title} · 副本", createdAt = now(), updatedAt = now(), revision = uid(), history = emptyList()); mutate { it.copy(notes = listOf(copy) + it.notes) }; mutable.update { it.copy(view = copy.notebookId, selectedId = copy.id) } }
    fun checkpoint(id: String) { mutate { w -> w.copy(notes = w.notes.map { if (it.id == id) it.copy(history = (listOf(it.snapshot()) + it.history).take(40), revision = uid(), updatedAt = now()) else it }) }; message("当前版本已保存") }
    fun restoreVersion(id: String, h: NoteVersion) { edit(id, true) { it.copy(title = h.title, content = h.content, username = h.username, password = h.password, url = h.url, otp = h.otp, tags = h.tags, variables = h.variables) }; message("历史版本已恢复") }
    fun preferences(change: (Preferences) -> Preferences) = mutate { it.copy(preferences = change(it.preferences)) }
    private fun cloudConfig(): SyncSettings {
        val s = mutable.value; require(s.workspace != null) { "本机数据尚未打开" }
        CloudSaveService.validateConfig(s.workspace.sync); return s.workspace.sync
    }
    suspend fun listCloudSaves(): SaveCatalog = CloudSaveService().list(cloudConfig())
    suspend fun writeCloudSave(catalog: SaveCatalog, name: String, overwrite: SaveEntry? = null): SaveCatalog {
        val config = cloudConfig(); flush(); return CloudSaveService().write(config, catalog, mutable.value.workspace ?: error("本机数据尚未打开"), name, overwrite)
    }
    suspend fun deleteCloudSave(catalog: SaveCatalog, entry: SaveEntry): SaveCatalog = CloudSaveService().delete(cloudConfig(), catalog, entry)
    suspend fun readCloudSave(catalog: SaveCatalog, entry: SaveEntry) {
        val config = cloudConfig(); val capturedEpoch = epoch; val payload = CloudSaveService().read(config, catalog, entry); replaceData(payload, capturedEpoch)
    }
    suspend fun replaceData(payload: SyncPayload, expectedEpoch: Long = epoch) {
        require(mutable.value.workspace != null) { "本机数据尚未打开" }
        loadingSave = true
        try {
            saveJob?.cancelAndJoin(); flush()
            saveMutex.withLock {
                val state = mutable.value
                require(epoch == expectedEpoch) { "空间已变化，请重新读取存档" }
                val candidate = GameSaves.apply(state.workspace ?: error("本机数据尚未打开"), payload)
                store.save(candidate); epoch++; serial++; savedSerial = serial; cursors.clear(); undo.clear(); redo.clear()
                mutable.value = state.copy(workspace = candidate, saving = false, saveError = false, view = "home", selectedId = null, editingCredentialId = null, search = "")
            }
        } finally { loadingSave = false }
        touch()
    }
    fun importText(name:String,text:String,notebookId:String?=null,markdown:Boolean=false) {
        val workspace=mutable.value.workspace?:run{pendingImport=TextImport(name,text,markdown);return}
        val book=workspace.liveBooks.firstOrNull{it.id==(notebookId?:mutable.value.view)&&it.mode!=NoteMode.Password}
        if(book==null){mutable.update{it.copy(textImport=TextImport(name,text,markdown))};return}
        val note=Note(notebookId=book.id,title=name,content=if(markdown)text else RichMarkdown.escape(text))
        mutate{it.copy(notes=it.notes+note)};mutable.update{it.copy(view=book.id,selectedId=note.id,textImport=null,editingCredentialId=null)}
    }
    fun cancelImport(){mutable.update{it.copy(textImport=null)}}
    suspend fun readBackup(envelope: Envelope, password: String) {
        val capturedEpoch = epoch
        val source = withContext(Dispatchers.Default) { Crypto.derive(password, envelope.salt, envelope.iterations).use { Crypto.decrypt<Workspace>(envelope, it).validate() } }
        replaceData(GameSaves.capture(source), capturedEpoch); message("备份已读取")
    }
    fun localBackup():String=json.encodeToString(GameSaves.capture(mutable.value.workspace?:error("本机数据尚未打开")))
    fun checkUpdate(notify: Boolean = true) = viewModelScope.launch { mutable.update { it.copy(updateChecking = true) }; try { val update = updater.check(); mutable.update { it.copy(update = update) }; if (notify) message(if (update == null) "你已在使用最新版本" else "发现新版本 ${update.version}") } catch (error: Exception) { if (notify) message(UserMessages.forError(error,"检查更新失败")) } finally { mutable.update { it.copy(updateChecking = false) } } }
    fun downloadUpdate() = viewModelScope.launch { val update = mutable.value.update ?: return@launch; mutable.update { it.copy(updateProgress = 0f) }; try { val file = updater.download(update) { progress -> mutable.update { it.copy(updateProgress = progress) } }; mutable.update { it.copy(downloadedApk = file) }; message("签名与 SHA-256 校验通过，可以安装") } catch (error: Exception) { message(UserMessages.forError(error,"更新下载失败")) } finally { mutable.update { it.copy(updateProgress = null) } } }
}
