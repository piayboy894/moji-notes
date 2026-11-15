package app.moji.android.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import app.moji.android.*
import app.moji.android.data.*
import app.moji.android.security.Crypto
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@Composable fun NotebookDialog(book: Notebook?, vm: MojiViewModel, close: () -> Unit) {
    var name by remember { mutableStateOf(book?.name ?: "") }; var mode by remember { mutableStateOf(book?.mode ?: NoteMode.Text) }; var selectedColor by remember { mutableStateOf(book?.color ?: "#75876D") }; var deleting by remember { mutableStateOf(false) }
    PanelDialog(if (book == null) "新建记事本" else "编辑记事本", close, actions = { if (book != null) TextButton(onClick = { deleting = true }) { Text("删除", color = MaterialTheme.colorScheme.error) }; Spacer(Modifier.weight(1f)); TextButton(onClick = close) { Text("取消") }; Button(onClick = { vm.saveBook(book, name, mode, selectedColor); close() }, enabled = name.isNotBlank()) { Text(if (book == null) "创建记事本" else "保存修改") } }) {
        Field("记事本名称", name) { name = it.take(40) }
        Text("记录模式", style = MaterialTheme.typography.labelLarge)
        Column { NoteMode.entries.forEach { type -> Row(Modifier.fillMaxWidth().clickable(enabled = book == null) { mode = type }, verticalAlignment = Alignment.CenterVertically) { RadioButton(mode == type, { mode = type }, enabled = book == null); Icon(modeIcon(type), null, Modifier.size(20.dp), tint = modeColor(type)); Spacer(Modifier.width(12.dp)); Text(type.label) } } }
        Text("封面颜色", style = MaterialTheme.typography.labelLarge)
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) { listOf("#75876D", "#B2946E", "#8393AC", "#A5849D", "#BA886C", "#9182A8").forEach { c -> Box(Modifier.size(32.dp).background(color(c), CircleShape).clickable { selectedColor = c }, contentAlignment = Alignment.Center) { if (selectedColor == c) Icon(MojiIcons.Check, "已选颜色", Modifier.size(18.dp), tint = androidx.compose.ui.graphics.Color.White) } } }
    }
    if (deleting && book != null) Confirm("删除「${book.name}」？", "其中的记录会移到回收站，之后仍可恢复。", { deleting = false }) { vm.deleteBook(book); close() }
}
@Composable fun ChooseNotebookDialog(s: AppState, vm: MojiViewModel, newBook: () -> Unit, close: () -> Unit) {
    PanelDialog("选择记事本", close) {
        s.workspace?.liveBooks?.forEach { book ->
            OutlinedCard(onClick = { vm.addNote(book.id); close() }, modifier = Modifier.fillMaxWidth().testTag("choose-book-${book.id}")) {
                Row(Modifier.padding(18.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                    Icon(modeIcon(book.mode), null, tint = modeColor(book.mode))
                    Column(Modifier.weight(1f)) { Text(book.name); Text(book.mode.label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    Icon(MojiIcons.ChevronRight, null)
                }
            }
        }
        TextButton(onClick = { close(); newBook() }) { Text("新建记事本") }
    }
}
@Composable fun PromptDialog(note: Note, activity: MainActivity, close: () -> Unit) {
    val variables=PromptVariables.used(note);var values by remember(note.id){mutableStateOf(emptyMap<String,String>())}
    PanelDialog("使用提示词",close,actions={Button(onClick={activity.copy(visibleText(activity,note,values))},enabled=variables.all{values[it.id]?.isNotBlank()==true}){Text("复制提示词")}}){
        variables.forEach{variable->Field(variable.name,values[variable.id]?:"",lines=2){values=values+(variable.id to it)}}
    }
}
@Composable fun HistoryDialog(note: Note, close: () -> Unit, allowRestore: Boolean = true, restore: (NoteVersion) -> Unit) {
    var selected by remember(note.id) { mutableStateOf(note.history.firstOrNull()) }; val date = remember { SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.getDefault()) }
    PanelDialog("版本历史", close, width = 760, actions = { Text("最近 40 个版本", Modifier.weight(1f), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant); Button(onClick = { selected?.let(restore) }, enabled = selected != null && allowRestore) { Text("恢复此版本") } }) {
        if (note.history.isEmpty()) Text("编辑后会自动保留历史版本", color = MaterialTheme.colorScheme.onSurfaceVariant)
        note.history.forEach { h -> OutlinedButton(onClick = { selected = h }, modifier = Modifier.fillMaxWidth(), border = androidx.compose.foundation.BorderStroke(1.dp, if (selected?.id == h.id) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant)) { Column(Modifier.weight(1f)) { Text(date.format(Date(h.at)), style = MaterialTheme.typography.labelLarge); Text(h.title, style = MaterialTheme.typography.labelSmall, maxLines = 1) }; if (selected?.id == h.id) Icon(MojiIcons.Check, null) } }
        selected?.let { h -> HorizontalDivider(); Text(h.title, style = MaterialTheme.typography.titleMedium); MarkdownPreview(h.content,15f,Modifier.fillMaxWidth().heightIn(max=250.dp)); if (h.password.isNotBlank()) Text("此版本包含密码", color = MaterialTheme.colorScheme.onSurfaceVariant, style = MaterialTheme.typography.labelSmall) }
    }
}
@Composable fun PasswordDialog(activity: MainActivity, close: () -> Unit, apply: (String) -> Unit) {
    var length by remember { mutableFloatStateOf(20f) }; var symbols by remember { mutableStateOf(true) }; var password by remember { mutableStateOf(Crypto.password()) }
    PanelDialog("生成强密码", close, actions = { TextButton(onClick = { activity.copy(password, true) }) { Text("复制") }; Button(onClick = { apply(password) }) { Text("使用此密码") } }) {
        Surface(color = MaterialTheme.colorScheme.surfaceVariant, shape = androidx.compose.foundation.shape.RoundedCornerShape(12.dp)) { Text(password, Modifier.fillMaxWidth().padding(19.dp), fontSize = 19.sp, fontFamily = FontFamily.Monospace) }
        Text("密码长度 · ${length.toInt()}")
        Slider(length, { length = it; password = Crypto.password(it.toInt(), symbols) }, valueRange = 12f..64f, steps = 51)
        Row(verticalAlignment = Alignment.CenterVertically) { Checkbox(symbols, { symbols = it; password = Crypto.password(length.toInt(), it) }); Text("包含特殊符号") }
        OutlinedButton(onClick = { password = Crypto.password(length.toInt(), symbols) }) { Icon(MojiIcons.Refresh, null); Spacer(Modifier.width(8.dp)); Text("重新生成") }
    }
}
@Composable fun FindDialog(content: String, close: () -> Unit, replace: (String) -> Unit, select: (Int, Int) -> Unit) {
    var query by remember { mutableStateOf("") }; var replacement by remember { mutableStateOf("") }; var caseSensitive by remember { mutableStateOf(false) }; var cursor by remember { mutableIntStateOf(0) }; var status by remember { mutableStateOf("") }
    fun find() { if (query.isEmpty()) return; var at = content.indexOf(query, cursor.coerceAtMost(content.length), !caseSensitive); if (at < 0) at = content.indexOf(query, ignoreCase = !caseSensitive); if (at < 0) status = "没有找到匹配内容" else { select(at, at + query.length); cursor = at + query.length; status = "已选中匹配内容" } }
    PanelDialog("查找与替换", close, actions = { TextButton(onClick = ::find, enabled = query.isNotEmpty()) { Text("下一个") }; Button(onClick = { if (query.isNotEmpty()) { val count = if (caseSensitive) content.windowed(query.length).count { it == query } else content.lowercase().windowed(query.length).count { it == query.lowercase() }; replace(content.replace(query, replacement, ignoreCase = !caseSensitive)); status = "已替换 $count 处" } }, enabled = query.isNotEmpty()) { Text("全部替换") } }) {
        Field("查找", query) { query = it; cursor = 0 }; Field("替换为", replacement) { replacement = it }
        Row(verticalAlignment = Alignment.CenterVertically) { Checkbox(caseSensitive, { caseSensitive = it }); Text("区分大小写") }
        if (status.isNotBlank()) Text(status, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}
@Composable fun GlobalSearch(s: AppState, vm: MojiViewModel, close: () -> Unit) {
    var query by remember { mutableStateOf("") }; val w = s.workspace ?: return
    val results = w.liveNotes.filter { n -> Records.matches(n, query, w.notebooks.first { it.id == n.notebookId }.mode) }.sortedByDescending { it.updatedAt }.take(50)
    PanelDialog("搜索你的空间", close, width = 700) {
        Field("搜索记录", query) { query = it }
        if (query.isBlank()) Row { TextButton(onClick = { vm.addNote(); close() }) { Icon(MojiIcons.Add, null); Text("新建记录") } }
        Text(if (query.isBlank()) "最近记录" else "${results.size} 条结果", color = MaterialTheme.colorScheme.onSurfaceVariant, style = MaterialTheme.typography.labelLarge)
        results.forEach { note -> Surface(onClick = { vm.navigate(note.notebookId); vm.select(note.id); close() }, color = androidx.compose.ui.graphics.Color.Transparent) { Row(Modifier.fillMaxWidth().padding(vertical = 13.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(13.dp)) { Icon(modeIcon(w.notebooks.first { it.id == note.notebookId }.mode), null); Column(Modifier.weight(1f)) { Text(note.title, maxLines = 1); Text(w.notebooks.first { it.id == note.notebookId }.name, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }; Icon(MojiIcons.NorthEast, null, Modifier.size(17.dp)) } } }
        if (results.isEmpty()) Text("没有找到相关记录", color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}
