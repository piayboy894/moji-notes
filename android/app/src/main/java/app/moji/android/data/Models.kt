package app.moji.android.data

import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import java.util.UUID

fun uid() = UUID.randomUUID().toString().replace("-", "")
fun now() = System.currentTimeMillis()
val json = Json { ignoreUnknownKeys = true; encodeDefaults = true; explicitNulls = false }

@Serializable enum class NoteMode { Text, Prompt, Password;
    val label get() = when (this) { Text -> "普通文本"; Prompt -> "提示词"; Password -> "账号密码" }
}
@Serializable data class Notebook(
    val id: String = uid(), val name: String = "新记事本", val mode: NoteMode = NoteMode.Text,
    val color: String = "#75876D", val createdAt: Long = now(), val updatedAt: Long = now(),
    val revision: String = uid(), val deletedAt: Long? = null
)
@Serializable data class NoteVersion(
    val id: String = uid(), val at: Long = now(), val title: String = "", val content: String = "",
    val username: String = "", val password: String = "", val url: String = "", val otp: String = "",
    val variables: List<PromptVariable> = emptyList(), val tags: List<String> = emptyList()
)
@Serializable data class Note(
    val id: String = uid(), val at: Long = now(), val title: String = "未命名记录", val content: String = "",
    val username: String = "", val password: String = "", val url: String = "", val otp: String = "",
    val variables: List<PromptVariable> = emptyList(), val tags: List<String> = emptyList(), val notebookId: String = "",
    val createdAt: Long = now(), val updatedAt: Long = now(), val revision: String = uid(),
    val favorite: Boolean = false, val pinned: Boolean = false, val archived: Boolean = false,
    val deletedAt: Long? = null, val purgedAt: Long? = null, val history: List<NoteVersion> = emptyList()
) {
    fun snapshot() = NoteVersion(title = title, content = content, username = username, password = password, url = url, otp = otp, tags = tags, variables = variables)
    fun edited(next: Note, checkpoint: Boolean = false): Note {
        val changed = title != next.title || content != next.content || username != next.username || password != next.password || url != next.url || otp != next.otp || variables != next.variables
        val versions = if (changed && (checkpoint || history.isEmpty() || now() - history.first().at > 60_000)) (listOf(snapshot()) + history).take(40) else history
        return next.copy(updatedAt = now(), revision = uid(), history = versions)
    }
}
@Serializable data class Preferences(
    val theme: String = "system", val fontSize: Double = 15.0, val lineNumbers: Boolean = false,
    val wordWrap: Boolean = true, val autoSync: Boolean = false,
    val editorView: String = "edit", val shortcuts: Map<String, String> = emptyMap()
)
@Serializable data class SyncSettings(
    val repository: String = "", val code: String = "", val token: String = "",
    val lastSync: Long = 0, val base: Map<String, String> = emptyMap()
)
@Serializable data class Workspace(
    val schema: Int = 1, val id: String = uid(), val notebooks: List<Notebook> = emptyList(),
    val notes: List<Note> = emptyList(), val preferences: Preferences = Preferences(), val sync: SyncSettings = SyncSettings()
) {
    val liveBooks get() = notebooks.filter { it.deletedAt == null }
    val liveNotes get() = notes.filter { n -> n.deletedAt == null && n.purgedAt == null && liveBooks.any { it.id == n.notebookId } }
    fun changeNote(id: String, checkpoint: Boolean = false, change: (Note) -> Note) = copy(notes = notes.map { if (it.id == id) it.edited(change(it), checkpoint) else it })
    fun validate(): Workspace {
        require(schema == 1 && notebooks.size <= 5000 && notes.size <= 100000) { "数据版本或容量不受支持" }
        require(notebooks.map { it.id }.distinct().size == notebooks.size && notes.map { it.id }.distinct().size == notes.size) { "数据 ID 重复" }
        require(notebooks.all { it.id.isNotBlank() && it.revision.isNotBlank() } && notes.all { n -> notebooks.any { it.id == n.notebookId } && n.id.isNotBlank() && n.revision.isNotBlank() }) { "记录数据损坏" }
        require(notes.all { n -> n.variables.map { it.id }.distinct().size == n.variables.size && n.variables.all { it.id.matches(Regex("[a-f0-9]{32}")) && it.name.isNotBlank() && it.name.length <= 40 } }) { "变量数据损坏" }
        require(preferences.theme in listOf("light", "dark", "system") && preferences.fontSize in 11.0..32.0) { "偏好设置无效" }
        return this
    }
}
@Serializable data class SyncPayload(val schema: Int = 1, val id: String, val notebooks: List<Notebook>, val notes: List<Note>)
fun Workspace.payload() = SyncPayload(schema, id, notebooks, notes)

object Records {
    fun strength(password: String) = (if (password.length >= 12) 1 else 0) + (if (password.length >= 18) 1 else 0) + (if (password.any(Char::isLowerCase) && password.any(Char::isUpperCase)) 1 else 0) + (if (password.any(Char::isDigit) && password.any { !it.isLetterOrDigit() }) 1 else 0)
    fun matches(note: Note, query: String, mode: NoteMode): Boolean = when {
        query.isBlank() -> true
        else -> listOf(note.title, note.username, note.url, if (mode == NoteMode.Password) "" else PromptVariables.display(note)).joinToString("\n").contains(query, true)
    }
    fun plain(content: String) = content.replace(Regex("!\\[[^]]*]\\([^)]*\\)"), "[图片]").replace(Regex("[#*`>_\\[\\]~]"), "").replace('\n', ' ').trim()
    fun relative(at: Long): String { val diff = (now() - at).coerceAtLeast(0); return when { diff < 60_000 -> "刚刚"; diff < 3_600_000 -> "${diff / 60_000} 分钟前"; diff < 86_400_000 -> "${diff / 3_600_000} 小时前"; else -> "${diff / 86_400_000} 天前" } }
    fun safeName(name: String) = name.replace(Regex("[\\\\/:*?\"<>|\\x00-\\x1f]"), "_").take(100).ifBlank { "未命名" }
}
object Seed {
    fun create() = Workspace(notebooks = listOf(
        Notebook(name = "日常随记"),
        Notebook(name = "灵感提示词", mode = NoteMode.Prompt, color = "#B2946E"),
        Notebook(name = "私人密码库", mode = NoteMode.Password, color = "#8393AC")
    ))
}
object NoteCreation {
    fun inNotebook(state: Workspace, notebookId: String): Note {
        val book = state.liveBooks.firstOrNull { it.id == notebookId } ?: error("请选择记事本")
        return Note(notebookId = book.id, title = when (book.mode) { NoteMode.Password -> "新账号"; NoteMode.Prompt -> "新提示词"; else -> "未命名记录" })
    }
}
