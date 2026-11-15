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

data class AppState(
    val workspace: Workspace? = null, val loading: Boolean = true, val exists: Boolean = false,
    val busy: Boolean = false, val demo: Boolean = false, val error: String = "", val saving: Boolean = false,
    val saveError: Boolean = false, val view: String = "home", val selectedId: String? = null,
    val search: String = "", val sort: String = "updated", val update: AndroidUpdate? = null,
    val updateChecking: Boolean = false, val updateProgress: Float? = null, val downloadedApk: File? = null,
    val biometricEnabled: Boolean = false
)
class MojiViewModel(application: Application) : AndroidViewModel(application) {
    val store = VaultStore(File(application.noBackupFilesDir, "vault"))
    val biometric = BiometricVault(application)
    val updater = AndroidUpdater(application)
    private val mutable = MutableStateFlow(AppState(biometricEnabled = biometric.enabled))
    val state = mutable.asStateFlow()
    private val notices = MutableSharedFlow<String>(extraBufferCapacity = 10)
    val messages = notices.asSharedFlow()
    var sessionKey: VaultKey? = null; private set
    private var saveJob: Job? = null; private var backgroundLock: Job? = null
    private val saveMutex = Mutex(); private var serial = 0L; private var savedSerial = 0L
    private var epoch = 0L; private var lastInput = now(); private var lastUpdateCheck = 0L
    private var locking = false
    private var loadingSave = false
    val cursors = mutableMapOf<String, Pair<Int, Int>>()
    val undo = mutableMapOf<String, ArrayDeque<String>>()
    val redo = mutableMapOf<String, ArrayDeque<String>>()
    var pendingImport: String? = null
    init {
        viewModelScope.launch { try { mutable.update { it.copy(exists = store.read() != null, loading = false) } } catch (_: Exception) { mutable.update { it.copy(loading = false, error = "本地数据无法读取，请从加密备份恢复") } } }
        viewModelScope.launch { while (isActive) { delay(1000); val s = mutable.value; val w = s.workspace ?: continue
            if (now() - lastInput > w.preferences.autoLockMinutes * 60_000L && !locking) lock()
        } }
    }
    fun message(text: String) { notices.tryEmit(text) }
    fun touch() { lastInput = now() }
    fun demo() { epoch++; mutable.update { AppState(workspace = Seed.create(true), demo = true, loading = false, exists = it.exists, biometricEnabled = biometric.enabled) }; touch() }
    fun authenticate(password: String, confirmation: String = "", backup: Envelope? = null) = viewModelScope.launch {
        if (mutable.value.busy) return@launch
        mutable.update { it.copy(busy = true, error = "") }
        try {
            val e = backup ?: store.read()
            val pair = withContext(Dispatchers.Default) {
                if (e != null) { val key = Crypto.derive(password, e.salt, e.iterations); try { Crypto.decrypt<Workspace>(e, key).validate() to key } catch (error: Exception) { key.close(); throw error } }
                else { require(password.length >= 10) { "主密码至少需要 10 个字符" }; require(password == confirmation) { "两次输入的主密码不一致" }; Seed.create() to Crypto.newKey(password) }
            }
            if (e == null || backup != null) { try { store.save(pair.first, pair.second) } catch (error: Exception) { pair.second.close(); throw error } }
            open(pair.first, pair.second)
            if (store.recoveredBackup) message("已恢复上一份加密备份")
        } catch (error: Exception) { mutable.update { it.copy(error = error.message ?: "解锁失败", busy = false) } }
    }
    fun biometricUnlock(key: VaultKey) = viewModelScope.launch {
        mutable.update { it.copy(busy = true, error = "") }
        try { val e = store.read() ?: error("找不到本地空间"); require(e.salt == key.salt) { "设备密钥已失效，请使用主密码" }; val workspace = withContext(Dispatchers.Default) { Crypto.decrypt<Workspace>(e, key).validate() }; open(workspace, key) }
        catch (_: Exception) { key.close(); biometric.disable(); mutable.update { it.copy(busy = false, biometricEnabled = false, error = "设备密钥已失效，请使用主密码") } }
    }
    private fun open(workspace: Workspace, key: VaultKey) {
        sessionKey?.close(); sessionKey = key; epoch++; serial = 0; savedSerial = 0
        mutable.value = AppState(workspace = workspace, loading = false, exists = true, biometricEnabled = biometric.enabled)
        touch()
        pendingImport?.let { pendingImport = null; importText("接收的文字", it) }
        if (now() - lastUpdateCheck > 6 * 3_600_000) { lastUpdateCheck = now(); checkUpdate(false) }
    }
    fun mutate(change: (Workspace) -> Workspace) {
        val s = mutable.value; val w = s.workspace ?: return
        if (locking || loadingSave) return
        serial++; mutable.value = s.copy(workspace = change(w), saving = !s.demo, saveError = false); touch()
        saveJob?.cancel(); if (!s.demo) saveJob = viewModelScope.launch { delay(350); try { flush() } catch (error: Exception) { mutable.update { it.copy(saveError = true, saving = false) }; message("保存失败，请先导出加密备份") } }
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
        val s = mutable.value; val key = sessionKey ?: return@withLock; val w = s.workspace ?: return@withLock
        if (s.demo || serial == savedSerial) return@withLock
        val captured = serial; store.save(w, key); savedSerial = captured; mutable.update { it.copy(saving = serial != savedSerial, saveError = false) }
    }
    fun background() { viewModelScope.launch { runCatching { flush() } }; backgroundLock?.cancel(); backgroundLock = viewModelScope.launch { delay(10_000); lock() } }
    fun foreground() { backgroundLock?.cancel(); touch() }
    fun lock() = viewModelScope.launch {
        if (locking || mutable.value.workspace == null) return@launch
        locking = true; mutable.update { it.copy(busy = true) }
        try { saveJob?.cancelAndJoin(); flush(); saveMutex.withLock { epoch++; sessionKey?.close(); sessionKey = null; cursors.clear(); undo.clear(); redo.clear(); mutable.value = AppState(loading = false, exists = store.file.exists(), biometricEnabled = biometric.enabled) } }
        catch (_: Exception) { mutable.update { it.copy(busy = false, saveError = true) }; message("锁定前保存失败，请先导出备份") } finally { locking = false }
    }
    fun navigate(view: String) { mutable.update { it.copy(view = view, selectedId = null, search = "") }; touch() }
    fun select(id: String) { mutable.update { it.copy(selectedId = id) }; touch() }
    fun clearSelection() { mutable.update { it.copy(selectedId = null) } }
    fun search(value: String) { mutable.update { it.copy(search = value) }; touch() }
    fun sort(value: String) { mutable.update { it.copy(sort = value) } }
    fun selectedNote() = mutable.value.workspace?.notes?.firstOrNull { it.id == mutable.value.selectedId }
    fun filtered(): List<Note> {
        val s = mutable.value; val w = s.workspace ?: return emptyList(); val books = w.notebooks.associateBy { it.id }
        val notes = w.notes.filter { n -> n.purgedAt == null && (if (s.view == "trash") n.deletedAt != null else n.deletedAt == null && books[n.notebookId]?.deletedAt == null && (if (s.view == "archive") n.archived else !n.archived)) && (s.view != "star" || n.favorite) && (w.liveBooks.none { it.id == s.view } || n.notebookId == s.view) && Records.matches(n, s.search, books[n.notebookId]?.mode ?: NoteMode.Text) }
        return when (s.sort) { "name" -> notes.sortedWith(compareByDescending<Note> { it.pinned }.thenBy { it.title }); "created" -> notes.sortedWith(compareByDescending<Note> { it.pinned }.thenByDescending { it.createdAt }); else -> notes.sortedWith(compareByDescending<Note> { it.pinned }.thenByDescending { it.updatedAt }) }
    }
    fun addNote(template: Triple<String, NoteMode, String>? = null) {
        val s = mutable.value; val w = s.workspace ?: return
        val mode = template?.second ?: w.liveBooks.firstOrNull { it.id == s.view }?.mode ?: NoteMode.Text
        var books = w.notebooks; val book = w.liveBooks.firstOrNull { it.id == s.view && it.mode == mode } ?: w.liveBooks.firstOrNull { it.mode == mode } ?: Notebook(name = mode.label, mode = mode).also { books = books + it }
        val note = Note(notebookId = book.id, title = template?.first ?: when (mode) { NoteMode.Password -> "新账号"; NoteMode.Prompt -> "新提示词"; else -> "未命名记录" }, content = template?.third ?: "")
        mutate { it.copy(notebooks = books, notes = listOf(note) + it.notes) }; mutable.update { it.copy(view = book.id, selectedId = note.id, search = "") }
    }
    fun saveBook(existing: Notebook?, name: String, mode: NoteMode, color: String) { if (name.isBlank()) return; if (existing == null) { val b = Notebook(name = name.trim(), mode = mode, color = color); mutate { it.copy(notebooks = it.notebooks + b) }; navigate(b.id) } else mutate { it.copy(notebooks = it.notebooks.map { b -> if (b.id == existing.id) b.copy(name = name.trim(), color = color, revision = uid(), updatedAt = now()) else b }) } }
    fun deleteBook(book: Notebook) { mutate { it.copy(notebooks = it.notebooks.map { b -> if (b.id == book.id) b.copy(deletedAt = now(), updatedAt = now(), revision = uid()) else b }, notes = it.notes.map { n -> if (n.notebookId == book.id && n.deletedAt == null) n.edited(n.copy(deletedAt = now())) else n }) }; navigate("home") }
    fun trash(ids: Set<String>) { mutate { it.copy(notes = it.notes.map { n -> if (n.id in ids) n.edited(n.copy(deletedAt = now())) else n }) }; mutable.update { it.copy(selectedId = null) }; message("已移到回收站") }
    fun restore(ids: Set<String>) { mutate { w -> val books = w.notes.filter { it.id in ids }.map { it.notebookId }.toSet(); w.copy(notebooks = w.notebooks.map { b -> if (b.id in books && b.deletedAt != null) b.copy(deletedAt = null, updatedAt = now(), revision = uid()) else b }, notes = w.notes.map { n -> if (n.id in ids) n.edited(n.copy(deletedAt = null)) else n }) }; mutable.update { it.copy(selectedId = null) }; message("记录已恢复") }
    fun purge(ids: Set<String>) { mutate { it.copy(notes = it.notes.map { n -> if (n.id in ids) n.copy(title = "", content = "", username = "", password = "", url = "", otp = "", tags = emptyList(), history = emptyList(), deletedAt = n.deletedAt ?: now(), purgedAt = now(), revision = uid(), updatedAt = now()) else n }) }; mutable.update { it.copy(selectedId = null) } }
    fun duplicate(note: Note) { val copy = note.copy(id = uid(), title = "${note.title} · 副本", createdAt = now(), updatedAt = now(), revision = uid(), history = emptyList()); mutate { it.copy(notes = listOf(copy) + it.notes) }; mutable.update { it.copy(view = copy.notebookId, selectedId = copy.id) } }
    fun checkpoint(id: String) { mutate { w -> w.copy(notes = w.notes.map { if (it.id == id) it.copy(history = (listOf(it.snapshot()) + it.history).take(40), revision = uid(), updatedAt = now()) else it }) }; message("当前版本已保存") }
    fun restoreVersion(id: String, h: NoteVersion) { edit(id, true) { it.copy(title = h.title, content = h.content, username = h.username, password = h.password, url = h.url, otp = h.otp, tags = h.tags) }; message("历史版本已恢复") }
    fun preferences(change: (Preferences) -> Preferences) = mutate { it.copy(preferences = change(it.preferences)) }
    private fun cloudConfig(): SyncSettings {
        val s = mutable.value; require(s.workspace != null && !s.demo && sessionKey != null) { "请先创建或解锁本机空间" }
        CloudSaveService.validateConfig(s.workspace.sync); return s.workspace.sync
    }
    suspend fun listCloudSaves(): SaveCatalog = CloudSaveService().list(cloudConfig())
    suspend fun writeCloudSave(catalog: SaveCatalog, name: String, overwrite: SaveEntry? = null): SaveCatalog {
        val config = cloudConfig(); flush(); return CloudSaveService().write(config, catalog, mutable.value.workspace ?: error("空间已锁定"), name, overwrite)
    }
    suspend fun deleteCloudSave(catalog: SaveCatalog, entry: SaveEntry): SaveCatalog = CloudSaveService().delete(cloudConfig(), catalog, entry)
    suspend fun readCloudSave(catalog: SaveCatalog, entry: SaveEntry) {
        val config = cloudConfig(); val capturedEpoch = epoch; val payload = CloudSaveService().read(config, catalog, entry); replaceData(payload, capturedEpoch)
    }
    suspend fun replaceData(payload: SyncPayload, expectedEpoch: Long = epoch) {
        require(mutable.value.workspace != null && sessionKey != null && !mutable.value.demo) { "请先创建或解锁本机空间" }
        loadingSave = true
        try {
            saveJob?.cancelAndJoin(); flush()
            saveMutex.withLock {
                val state = mutable.value; val key = sessionKey ?: error("空间已锁定")
                require(epoch == expectedEpoch) { "空间已变化，请重新读取存档" }
                val candidate = GameSaves.apply(state.workspace ?: error("空间已锁定"), payload)
                store.save(candidate, key); epoch++; serial++; savedSerial = serial; cursors.clear(); undo.clear(); redo.clear()
                mutable.value = state.copy(workspace = candidate, saving = false, saveError = false, view = "home", selectedId = null, search = "")
            }
        } finally { loadingSave = false }
        touch()
    }
    fun importText(name: String, text: String) { val w = mutable.value.workspace ?: run { pendingImport = text; return }; var books = w.notebooks; val book = w.liveBooks.firstOrNull { it.id == mutable.value.view && it.mode != NoteMode.Password } ?: w.liveBooks.firstOrNull { it.mode == NoteMode.Text } ?: Notebook(name = "导入文本").also { books = books + it }; val note = Note(notebookId = book.id, title = name, content = text); mutate { it.copy(notebooks = books, notes = it.notes + note) }; mutable.update { it.copy(view = book.id, selectedId = note.id) } }
    suspend fun readBackup(envelope: Envelope, password: String) {
        val capturedEpoch = epoch
        val source = withContext(Dispatchers.Default) { Crypto.derive(password, envelope.salt, envelope.iterations).use { Crypto.decrypt<Workspace>(envelope, it).validate() } }
        replaceData(GameSaves.capture(source), capturedEpoch); message("备份已读取")
    }
    suspend fun encryptedBackup(): String { val w = mutable.value.workspace ?: error("请先解锁"); val key = sessionKey ?: error("演示空间不导出加密备份"); return saveMutex.withLock { withContext(Dispatchers.Default) { json.encodeToString(Crypto.encrypt(w, key)) } } }
    suspend fun changePassword(old: String, next: String) { require(next.length >= 10) { "新主密码至少需要 10 个字符" }; flush(); val e = store.read() ?: error("本地文件不存在"); withContext(Dispatchers.Default) { Crypto.derive(old, e.salt, e.iterations).use { Crypto.decrypt<Workspace>(e, it) } }; val fresh = withContext(Dispatchers.Default) { Crypto.newKey(next) }; saveMutex.withLock { try { store.save(mutable.value.workspace ?: error("空间已锁定"), fresh); sessionKey?.close(); sessionKey = fresh; biometric.disable(); mutable.update { it.copy(biometricEnabled = false) } } catch (error: Exception) { fresh.close(); throw error } }; message("主密码已更换") }
    fun biometricChanged() { mutable.update { it.copy(biometricEnabled = biometric.enabled) } }
    fun checkUpdate(notify: Boolean = true) = viewModelScope.launch { mutable.update { it.copy(updateChecking = true) }; try { val update = updater.check(); mutable.update { it.copy(update = update) }; if (notify) message(if (update == null) "你已在使用最新版本" else "发现新版本 ${update.version}") } catch (error: Exception) { if (notify) message(error.message ?: "检查更新失败") } finally { mutable.update { it.copy(updateChecking = false) } } }
    fun downloadUpdate() = viewModelScope.launch { val update = mutable.value.update ?: return@launch; mutable.update { it.copy(updateProgress = 0f) }; try { val file = updater.download(update) { progress -> mutable.update { it.copy(updateProgress = progress) } }; mutable.update { it.copy(downloadedApk = file) }; message("签名与 SHA-256 校验通过，可以安装") } catch (error: Exception) { message(error.message ?: "更新下载失败") } finally { mutable.update { it.copy(updateProgress = null) } } }
    override fun onCleared() { sessionKey?.close(); super.onCleared() }
}
