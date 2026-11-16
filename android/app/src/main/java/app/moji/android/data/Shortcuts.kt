package app.moji.android.data

object Shortcuts {
    val actions = listOf(
        Triple("recordCopy", "复制记录", "Ctrl+C"), Triple("recordCut", "剪切记录", "Ctrl+X"), Triple("recordPaste", "粘贴记录", "Ctrl+V"),
        Triple("search", "全局搜索", "Ctrl+K"), Triple("new", "新建记录", "Ctrl+N"),
        Triple("save", "保存历史版本", "Ctrl+S"), Triple("cloud", "云存档", "Ctrl+Shift+S"),
        Triple("undo", "撤销", "Ctrl+Z"), Triple("redo", "重做", "Ctrl+Y"), Triple("bold", "加粗", "Ctrl+B")
    )
    fun binding(p: Preferences, action: String) = p.shortcuts[action] ?: actions.first { it.first == action }.third
    fun resolve(p: Preferences, chord: String) = if (chord.isEmpty()) null else actions.firstOrNull { binding(p, it.first) == chord }?.first
    fun error(p: Preferences, action: String, chord: String): String? {
        if (chord.isEmpty()) return null
        if (!Regex("(?:(?:Ctrl\\+)(?:Alt\\+)?(?:Shift\\+)?|(?:Alt\\+)(?:Shift\\+)?)(?:[A-Z0-9]|F(?:[1-9]|1[0-2]))|F(?:[1-9]|1[0-2])").matches(chord)) return "请使用 Ctrl / Alt 组合键或 F1–F12"
        if (chord in listOf("Ctrl+C", "Ctrl+V", "Ctrl+X", "Ctrl+A", "Alt+F4") && actions.none{it.first==action&&it.first.startsWith("record")&&it.third==chord}) return "此组合用于系统或基础编辑"
        return actions.firstOrNull { it.first != action && binding(p, it.first) == chord }?.let { "已用于「${it.second}」" }
    }
}
