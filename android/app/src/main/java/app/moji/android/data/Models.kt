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
    val tags: List<String> = emptyList()
)
@Serializable data class Note(
    val id: String = uid(), val at: Long = now(), val title: String = "未命名记录", val content: String = "",
    val username: String = "", val password: String = "", val url: String = "", val otp: String = "",
    val tags: List<String> = emptyList(), val notebookId: String = "",
    val createdAt: Long = now(), val updatedAt: Long = now(), val revision: String = uid(),
    val favorite: Boolean = false, val pinned: Boolean = false, val archived: Boolean = false,
    val deletedAt: Long? = null, val purgedAt: Long? = null, val history: List<NoteVersion> = emptyList()
) {
    fun snapshot() = NoteVersion(title = title, content = content, username = username, password = password, url = url, otp = otp, tags = tags)
    fun edited(next: Note, checkpoint: Boolean = false): Note {
        val changed = title != next.title || content != next.content || username != next.username || password != next.password || url != next.url || otp != next.otp
        val versions = if (changed && (checkpoint || history.isEmpty() || now() - history.first().at > 60_000)) (listOf(snapshot()) + history).take(40) else history
        return next.copy(updatedAt = now(), revision = uid(), history = versions)
    }
}
@Serializable data class Preferences(
    val theme: String = "system", val fontSize: Double = 15.0, val lineNumbers: Boolean = false,
    val wordWrap: Boolean = true, val autoLockMinutes: Int = 10, val autoSync: Boolean = false,
    val editorView: String = "edit"
)
@Serializable data class SyncSettings(
    val repository: String = "piayboy894/xuxian-data", val code: String = "", val token: String = "",
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
        require(preferences.theme in listOf("light", "dark", "system") && preferences.autoLockMinutes in 1..240 && preferences.fontSize in 11.0..32.0) { "偏好设置无效" }
        return this
    }
}
@Serializable data class SyncPayload(val schema: Int = 1, val id: String, val notebooks: List<Notebook>, val notes: List<Note>)
fun Workspace.payload() = SyncPayload(schema, id, notebooks, notes)

object Records {
    private val variable = Regex("\\{\\{\\s*([^{}]+?)\\s*\\}\\}")
    fun variables(content: String) = variable.findAll(content).map { it.groupValues[1] }.distinct().toList()
    fun fill(content: String, values: Map<String, String>) = variable.replace(content) { values[it.groupValues[1]] ?: it.value }
    fun strength(password: String) = (if (password.length >= 12) 1 else 0) + (if (password.length >= 18) 1 else 0) + (if (password.any(Char::isLowerCase) && password.any(Char::isUpperCase)) 1 else 0) + (if (password.any(Char::isDigit) && password.any { !it.isLetterOrDigit() }) 1 else 0)
    fun matches(note: Note, query: String, mode: NoteMode): Boolean = when {
        query.isBlank() -> true
        query.startsWith('#') -> note.tags.any { it.contains(query.drop(1), true) }
        else -> listOf(note.title, note.username, note.url, note.tags.joinToString(" "), if (mode == NoteMode.Password) "" else note.content).joinToString("\n").contains(query, true)
    }
    fun plain(content: String) = content.replace(Regex("!\\[[^]]*]\\([^)]*\\)"), "[图片]").replace(Regex("[#*`>_\\[\\]~]"), "").replace('\n', ' ').trim()
    fun relative(at: Long): String { val diff = (now() - at).coerceAtLeast(0); return when { diff < 60_000 -> "刚刚"; diff < 3_600_000 -> "${diff / 60_000} 分钟前"; diff < 86_400_000 -> "${diff / 3_600_000} 小时前"; else -> "${diff / 86_400_000} 天前" } }
    fun safeName(name: String) = name.replace(Regex("[\\\\/:*?\"<>|\\x00-\\x1f]"), "_").take(100).ifBlank { "未命名" }
}
object Seed {
    fun create(demo: Boolean = false): Workspace {
        val books = listOf(Notebook(name = "日常随记"), Notebook(name = "灵感提示词", mode = NoteMode.Prompt, color = "#B2946E"), Notebook(name = "私人密码库", mode = NoteMode.Password, color = "#8393AC"))
        val notes = mutableListOf(
            Note(notebookId = books[0].id, title = "把生活，写成喜欢的样子", content = "# 把生活，写成喜欢的样子\n\n记录不必宏大。一个想法，一段文字，一次小小的发现，都值得拥有自己的位置。\n\n> 慢慢记录，慢慢成为自己。\n\n## 这个星期的小事\n\n- [x] 整理桌面，让想法自由呼吸\n- [ ] 留一个下午，读完那本书\n\n## 值得收藏的事物\n\n| 片刻 | 想留下的东西 |\n| --- | --- |\n| 清晨 | 第一杯咖啡的香气 |\n| 午后 | 窗边移动的光 |", tags = listOf("生活", "灵感"), favorite = true, pinned = true),
            Note(notebookId = books[1].id, title = "让文字更有温度", content = "你是一位擅长细腻表达的中文编辑。\n\n请润色以下文字：\n\n{{原始文字}}\n\n要求：\n1. 保留原意，使用自然简洁的中文。\n2. 删除重复和空洞的表达。\n3. 适合发布在 {{发布平台}}。\n\n请直接输出润色后的版本。", tags = listOf("写作", "润色"), favorite = true)
        )
        if (demo) notes += listOf(
            Note(notebookId = books[0].id, title = "周末，留一点空白", content = "## 周末清单\n\n- [ ] 去常去的那家书店\n- [ ] 带相机沿河走走\n\n给自己一点不被安排的时间。", tags = listOf("生活"), updatedAt = now() - 3_600_000),
            Note(notebookId = books[1].id, title = "产品设计 · 从想法到方案", content = "你是一位产品设计师。\n\n请围绕 {{产品想法}}，面向 {{目标用户}}，输出用户场景、信息架构和核心流程。", tags = listOf("设计", "产品")),
            Note(notebookId = books[2].id, title = "示例 · GitHub", username = "hello@example.com", password = "Example-Only-2026!", url = "https://github.com", tags = listOf("开发"))
        )
        return Workspace(notebooks = books, notes = notes)
    }
    val templates = listOf(
        Triple("空白文本", NoteMode.Text, ""), Triple("提示词模板", NoteMode.Prompt, "你是一位 {{角色}}。\n\n## 任务\n\n{{任务内容}}\n\n## 要求\n\n面向 {{目标受众}}，以 {{输出格式}} 输出。"), Triple("账号密码", NoteMode.Password, ""),
        Triple("每周计划", NoteMode.Text, "## 本周重点\n\n- [ ] \n- [ ] \n- [ ] \n\n## 每日安排\n\n| 日期 | 安排 |\n| --- | --- |\n| 周一 | |\n| 周二 | |\n| 周三 | |\n| 周四 | |\n| 周五 | |\n\n## 本周回顾\n\n"),
        Triple("会议记录", NoteMode.Text, "## 会议信息\n\n日期：\n参与人：\n\n## 讨论要点\n\n1. \n\n## 行动事项\n\n- [ ] 负责人 · 事项 · 截止日期\n"),
        Triple("阅读笔记", NoteMode.Text, "## 书籍信息\n\n书名：\n作者：\n\n## 值得记住的话\n\n> \n\n## 我的想法\n\n")
    )
}
