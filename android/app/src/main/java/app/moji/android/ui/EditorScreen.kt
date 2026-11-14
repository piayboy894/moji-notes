package app.moji.android.ui

import android.graphics.BitmapFactory
import android.content.Intent
import android.net.Uri
import android.widget.TextView
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.input.key.*
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.*
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.*
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import app.moji.android.*
import app.moji.android.data.*
import app.moji.android.security.Crypto
import app.moji.android.security.Totp
import io.noties.markwon.*
import io.noties.markwon.ext.tables.TablePlugin
import io.noties.markwon.ext.tasklist.TaskListPlugin
import io.noties.markwon.ext.strikethrough.StrikethroughPlugin
import kotlinx.coroutines.delay

@Composable fun EditorScreen(vm: MojiViewModel, s: AppState, note: Note, activity: MainActivity, export: (Note) -> Unit, image: () -> Unit, modifier: Modifier = Modifier, showBack: Boolean = false, back: () -> Unit = {}) {
    val w = s.workspace ?: return; val book = w.notebooks.first { it.id == note.notebookId }
    var more by remember(note.id) { mutableStateOf(false) }; var history by remember(note.id) { mutableStateOf(false) }; var prompt by remember(note.id) { mutableStateOf(false) }; var move by remember(note.id) { mutableStateOf(false) }; var outline by remember(note.id) { mutableStateOf(false) }; var find by remember(note.id) { mutableStateOf(false) }; var link by remember(note.id) { mutableStateOf(false) }; var purge by remember(note.id) { mutableStateOf(false) }; var focus by remember { mutableStateOf(false) }
    val font = w.preferences.fontSize.sp
    var editMode by remember(note.id) { mutableStateOf(w.preferences.editorView) }
    var value by remember(note.id) { mutableStateOf(TextFieldValue(note.content, vm.cursors[note.id]?.let { TextRange(it.first.coerceIn(0, note.content.length), it.second.coerceIn(0, note.content.length)) } ?: TextRange.Zero)) }
    LaunchedEffect(note.content) { if (value.text != note.content) value = value.copy(text = note.content, selection = TextRange(value.selection.start.coerceIn(0, note.content.length), value.selection.end.coerceIn(0, note.content.length))) }
    fun change(next: TextFieldValue) { value = next; vm.cursors[note.id] = next.selection.start to next.selection.end; vm.content(note.id, next.text) }
    fun insert(before: String, after: String = "", fallback: String = "") { val from = value.selection.min; val to = value.selection.max; val selected = value.text.substring(from, to).ifBlank { fallback }; val replacement = before + selected + after; change(TextFieldValue(value.text.replaceRange(from, to, replacement), TextRange(from + before.length, from + before.length + selected.length))) }
    BoxWithConstraints(modifier.fillMaxSize().background(MaterialTheme.colorScheme.surface).onPreviewKeyEvent { event -> if (event.type == KeyEventType.KeyDown && event.isCtrlPressed) when (event.key) { Key.B -> { insert("**", "**", "粗体"); true }; Key.F, Key.H -> { find = true; true }; Key.Z -> { vm.undo(note.id); true }; Key.Y -> { vm.redo(note.id); true }; else -> false } else false }) {
        val roomy = maxWidth >= 900.dp
        Column(Modifier.fillMaxSize()) {
            if (!focus) {
                Row(Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                    if (showBack) Tool(Icons.Outlined.ArrowBack, "返回记录", action = back)
                    Text(book.name, Modifier.weight(1f), maxLines = 1, overflow = TextOverflow.Ellipsis, style = MaterialTheme.typography.labelSmall, color = color(book.color))
                    if (note.deletedAt == null) { Tool(if (note.favorite) Icons.Outlined.Star else Icons.Outlined.StarOutline, "收藏记录") { vm.edit(note.id) { it.copy(favorite = !it.favorite) } }; Tool(Icons.Outlined.History, "版本历史") { history = true } }
                    Box { Tool(Icons.Outlined.MoreHoriz, "更多操作") { more = true }; DropdownMenu(more, { more = false }) {
                        if (note.deletedAt == null) {
                            DropdownMenuItem(text = { Text(if (note.pinned) "取消置顶" else "置顶记录") }, leadingIcon = { Icon(Icons.Outlined.PushPin, null) }, onClick = { vm.edit(note.id) { it.copy(pinned = !it.pinned) }; more = false })
                            DropdownMenuItem(text = { Text("创建副本") }, leadingIcon = { Icon(Icons.Outlined.ContentCopy, null) }, onClick = { vm.duplicate(note); more = false })
                            DropdownMenuItem(text = { Text(if (note.archived) "取消归档" else "归档记录") }, leadingIcon = { Icon(Icons.Outlined.Archive, null) }, onClick = { vm.edit(note.id) { it.copy(archived = !it.archived) }; more = false; vm.clearSelection() })
                            DropdownMenuItem(text = { Text("移动到记事本") }, leadingIcon = { Icon(Icons.Outlined.DriveFileMove, null) }, onClick = { move = true; more = false })
                            DropdownMenuItem(text = { Text("保存当前版本") }, leadingIcon = { Icon(Icons.Outlined.Save, null) }, onClick = { vm.checkpoint(note.id); more = false })
                            if (book.mode != NoteMode.Password) {
                                DropdownMenuItem(text = { Text("导出 Markdown") }, leadingIcon = { Icon(Icons.Outlined.FileDownload, null) }, onClick = { export(note); more = false })
                                DropdownMenuItem(text = { Text("分享记录") }, leadingIcon = { Icon(Icons.Outlined.Share, null) }, onClick = { activity.share(note.title, note.content); more = false })
                                DropdownMenuItem(text = { Text("复制全文") }, leadingIcon = { Icon(Icons.Outlined.CopyAll, null) }, onClick = { activity.copy(note.content); more = false })
                                DropdownMenuItem(text = { Text("文档大纲") }, leadingIcon = { Icon(Icons.Outlined.Segment, null) }, onClick = { outline = true; more = false })
                            }
                            DropdownMenuItem(text = { Text("专注模式") }, leadingIcon = { Icon(Icons.Outlined.Fullscreen, null) }, onClick = { focus = true; more = false })
                            HorizontalDivider()
                            DropdownMenuItem(text = { Text("移到回收站", color = MaterialTheme.colorScheme.error) }, leadingIcon = { Icon(Icons.Outlined.DeleteOutline, null) }, onClick = { vm.trash(setOf(note.id)); more = false })
                        } else DropdownMenuItem(text = { Text("永久删除", color = MaterialTheme.colorScheme.error) }, onClick = { more = false; purge = true })
                    } }
                }
                Column(Modifier.fillMaxWidth().padding(horizontal = 23.dp).padding(top = 9.dp, bottom = 18.dp), verticalArrangement = Arrangement.spacedBy(9.dp)) {
                    BasicTextField(note.title, { text -> vm.edit(note.id) { it.copy(title = text) } }, Modifier.fillMaxWidth().testTag("note-title"), textStyle = MaterialTheme.typography.titleLarge.copy(color = MaterialTheme.colorScheme.onSurface), cursorBrush = SolidColor(MaterialTheme.colorScheme.primary), enabled = note.deletedAt == null, singleLine = false)
                    var tags by remember(note.id, note.tags) { mutableStateOf(note.tags.joinToString(" ")) }
                    BasicTextField(tags, { tags = it; vm.edit(note.id) { n -> n.copy(tags = it.split(Regex("[\\s,，;；]+")).map { t -> t.trimStart('#') }.filter(String::isNotBlank).distinct().take(30)) } }, Modifier.fillMaxWidth().testTag("note-tags"), textStyle = MaterialTheme.typography.labelSmall.copy(color = MaterialTheme.colorScheme.onSurfaceVariant), cursorBrush = SolidColor(MaterialTheme.colorScheme.primary), singleLine = true, enabled = note.deletedAt == null, decorationBox = { inner -> if (tags.isEmpty()) Text("+ 添加标签（空格分隔）", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.outline); inner() })
                }
            } else Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) { Tool(Icons.Outlined.FullscreenExit, "退出专注模式") { focus = false } }
            if (note.deletedAt != null) {
                Column(Modifier.weight(1f).fillMaxWidth().padding(30.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(25.dp)) { Icon(Icons.Outlined.DeleteOutline, null, Modifier.size(45.dp), tint = MaterialTheme.colorScheme.outline); Text("这条记录在回收站中", color = MaterialTheme.colorScheme.onSurfaceVariant); Button(onClick = { vm.restore(setOf(note.id)) }) { Icon(Icons.Outlined.Restore, null); Spacer(Modifier.width(8.dp)); Text("恢复记录") }; TextButton(onClick = { purge = true }) { Text("永久删除", color = MaterialTheme.colorScheme.error) }; if (book.mode != NoteMode.Password) Text(note.content.take(2000), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            } else if (book.mode == NoteMode.Password) CredentialEditor(vm, note, activity, Modifier.weight(1f))
            else {
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).background(MaterialTheme.colorScheme.background).padding(horizontal = 9.dp), verticalAlignment = Alignment.CenterVertically) {
                    listOf(Triple(Icons.Outlined.Title, "标题", "## "), Triple(Icons.Outlined.FormatBold, "粗体", "**"), Triple(Icons.Outlined.FormatItalic, "斜体", "*"), Triple(Icons.Outlined.FormatStrikethrough, "删除线", "~~"), Triple(Icons.Outlined.FormatQuote, "引用", "> "), Triple(Icons.Outlined.FormatListBulleted, "无序列表", "- "), Triple(Icons.Outlined.FormatListNumbered, "有序列表", "1. "), Triple(Icons.Outlined.Checklist, "待办清单", "- [ ] "), Triple(Icons.Outlined.Code, "代码块", "\n```\n")).forEach { (icon, label, before) -> Tool(icon, label, editMode != "preview") { insert(before, when (before) { "**", "*", "~~" -> before; "\n```\n" -> "\n```\n"; else -> "" }, label) } }
                    Tool(Icons.Outlined.Link, "插入链接", editMode != "preview") { link = true }; Tool(Icons.Outlined.Image, "插入图片", editMode != "preview", image); Tool(Icons.Outlined.TableChart, "插入表格", editMode != "preview") { insert("\n| 标题 | 标题 |\n| --- | --- |\n| ", " | 内容 |\n", "内容") }; Tool(Icons.Outlined.Undo, "撤销") { vm.undo(note.id) }; Tool(Icons.Outlined.Redo, "重做") { vm.redo(note.id) }; Tool(Icons.Outlined.FindReplace, "查找与替换") { find = true }
                }
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                if (book.mode == NoteMode.Prompt) Surface(color = MaterialTheme.colorScheme.secondaryContainer.copy(alpha = .45f)) { Row(Modifier.fillMaxWidth().padding(horizontal = 21.dp, vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) { Text("${Records.variables(note.content).size} 个模板变量", Modifier.weight(1f), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.secondary); TextButton(onClick = { if (Records.variables(note.content).isEmpty()) activity.copy(note.content) else prompt = true }) { Icon(Icons.Outlined.AutoAwesome, null, Modifier.size(17.dp)); Spacer(Modifier.width(6.dp)); Text("使用提示词") } } }
                Row(Modifier.weight(1f).fillMaxWidth()) {
                    if (editMode != "preview" || (editMode == "split" && !roomy)) {
                        Row(Modifier.weight(1f).fillMaxHeight().verticalScroll(rememberScrollState()).padding(23.dp, 20.dp)) {
                            if (w.preferences.lineNumbers) Text((1..value.text.count { it == '\n' } + 1).joinToString("\n"), Modifier.padding(end = 13.dp), style = MaterialTheme.typography.bodyLarge.copy(fontFamily = FontFamily.Monospace, fontSize = font, lineHeight = font * 1.85f), color = MaterialTheme.colorScheme.outline)
                            BasicTextField(value, ::change, Modifier.fillMaxWidth().heightIn(min = 200.dp).testTag("note-content"), textStyle = MaterialTheme.typography.bodyLarge.copy(color = MaterialTheme.colorScheme.onSurface, fontSize = font, lineHeight = font * 1.85f, fontFamily = FontFamily.Monospace), cursorBrush = SolidColor(MaterialTheme.colorScheme.primary), visualTransformation = MarkdownTransformation(MaterialTheme.colorScheme.primary, MaterialTheme.colorScheme.secondary), keyboardOptions = KeyboardOptions(autoCorrectEnabled = false), decorationBox = { inner -> if (value.text.isEmpty()) Text("输入正文", color = MaterialTheme.colorScheme.outline); inner() })
                        }
                    }
                    if (editMode == "preview" || (editMode == "split" && roomy)) { if (editMode == "split") VerticalDivider(color = MaterialTheme.colorScheme.outlineVariant); MarkdownPreview(note.content, w.preferences.fontSize.toFloat(), Modifier.weight(1f).fillMaxHeight()) }
                }
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                Row(Modifier.fillMaxWidth().padding(horizontal = 12.dp), verticalAlignment = Alignment.CenterVertically) {
                    TextButton(onClick = { editMode = "edit" }) { Text("编辑", color = if (editMode != "preview") MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant) }
                    if (roomy) TextButton(onClick = { editMode = "split" }) { Text("双栏") }
                    TextButton(onClick = { editMode = "preview" }) { Text("阅读", color = if (editMode == "preview") MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant) }
                    Spacer(Modifier.weight(1f)); Text("${note.content.count { !it.isWhitespace() }} 字 · ${note.content.count { it == '\n' } + 1} 行", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
    if (history) HistoryDialog(note, { history = false }) { vm.restoreVersion(note.id, it); history = false }
    if (prompt) PromptDialog(note, activity, { prompt = false })
    if (find) FindDialog(note.content, { find = false }, { text -> vm.content(note.id, text) }, { start, end -> value = value.copy(selection = TextRange(start, end)); vm.cursors[note.id] = start to end })
    if (outline) PanelDialog("文档大纲", { outline = false }) { val headings = Regex("^(#{1,6})\\s+(.+)$", RegexOption.MULTILINE).findAll(note.content).toList(); if (headings.isEmpty()) Text("文档中尚无标题", color = MaterialTheme.colorScheme.onSurfaceVariant); headings.forEach { h -> TextButton(onClick = { value = value.copy(selection = TextRange(h.range.first)); outline = false; editMode = "edit" }) { Text("    ".repeat(h.groupValues[1].length - 1) + h.groupValues[2]) } } }
    if (move) PanelDialog("移动到记事本", { move = false }) { val books = w.liveBooks.filter { it.mode == book.mode && it.id != book.id }; if (books.isEmpty()) Text("暂无其他同模式记事本"); books.forEach { target -> TextButton(onClick = { vm.edit(note.id) { it.copy(notebookId = target.id) }; vm.navigate(target.id); vm.select(note.id); move = false }) { Icon(modeIcon(target.mode), null); Spacer(Modifier.width(12.dp)); Text(target.name) } } }
    if (link) { var text by remember { mutableStateOf("") }; var url by remember { mutableStateOf("https://") }; PanelDialog("插入链接", { link = false }, actions = { Button(onClick = { if (url.startsWith("https://") || url.startsWith("http://")) { insert("[${text.ifBlank { "链接" }}](${url.replace(")", "%29")})"); link = false } }) { Text("插入") } }) { Field("显示文字", text) { text = it }; Field("链接地址", url) { url = it } } }
    if (purge) Confirm("永久删除这条记录？", "正文、密码和历史版本会被清除，无法恢复。", { purge = false }) { vm.purge(setOf(note.id)) }
}
private class MarkdownTransformation(private val heading: Color, private val variable: Color) : VisualTransformation {
    override fun filter(text: AnnotatedString): TransformedText { val result = buildAnnotatedString { append(text); Regex("^#{1,6}\\s.+$", RegexOption.MULTILINE).findAll(text).forEach { addStyle(SpanStyle(color = heading, fontWeight = FontWeight.SemiBold), it.range.first, it.range.last + 1) }; Regex("\\{\\{[^{}]+\\}\\}").findAll(text).forEach { addStyle(SpanStyle(color = variable, fontWeight = FontWeight.Medium), it.range.first, it.range.last + 1) } }; return TransformedText(result, OffsetMapping.Identity) }
}
@Composable fun MarkdownPreview(content: String, size: Float, modifier: Modifier = Modifier) {
    val context = LocalContext.current; val ink = MaterialTheme.colorScheme.onSurface
    val markwon = remember(context) {
        Markwon.builder(context)
            .usePlugin(TablePlugin.create(context))
            .usePlugin(TaskListPlugin.create(context))
            .usePlugin(StrikethroughPlugin.create())
            .usePlugin(object : AbstractMarkwonPlugin() {
                override fun configureConfiguration(builder: MarkwonConfiguration.Builder) {
                    builder.linkResolver { view, link ->
                        val uri = Uri.parse(link)
                        if (uri.scheme in listOf("http", "https")) {
                            runCatching { view.context.startActivity(Intent(Intent.ACTION_VIEW, uri)) }
                        }
                    }
                }
            }).build()
    }
    val images = remember(content) { Regex("!\\[[^]]*]\\((data:image/(?:png|jpeg|webp|gif);base64,[A-Za-z0-9+/=]+)\\)").findAll(content).mapNotNull { match -> runCatching { val bytes = Crypto.unb64(match.groupValues[1].substringAfter(',')); if (bytes.size > 2_000_000) null else BitmapFactory.decodeByteArray(bytes, 0, bytes.size)?.asImageBitmap() }.getOrNull() }.toList() }
    Column(modifier.verticalScroll(rememberScrollState()).padding(23.dp, 20.dp), verticalArrangement = Arrangement.spacedBy(15.dp)) {
        AndroidView(factory = { TextView(it).apply { setTextIsSelectable(true); setPadding(0, 0, 0, 0); setLineSpacing(0f, 1.5f) } }, update = { view -> view.textSize = size; view.setTextColor(android.graphics.Color.argb(255, (ink.red * 255).toInt(), (ink.green * 255).toInt(), (ink.blue * 255).toInt())); markwon.setMarkdown(view, content.replace(Regex("!\\[([^]]*)]\\(data:image/[^)]+\\)"), "[图片：$1]")) }, modifier = Modifier.fillMaxWidth().testTag("markdown-preview"))
        images.forEach { Image(it, "内嵌图片", Modifier.fillMaxWidth().heightIn(max = 400.dp)) }
    }
}
@Composable private fun CredentialEditor(vm: MojiViewModel, note: Note, activity: MainActivity, modifier: Modifier = Modifier) {
    var generator by remember { mutableStateOf(false) }; var otp by remember(note.id) { mutableStateOf("") }; var remaining by remember { mutableIntStateOf(30) }
    LaunchedEffect(note.otp) { while (true) { remaining = 30 - ((now() / 1000) % 30).toInt(); otp = if (note.otp.isBlank()) "" else runCatching { Totp.code(note.otp) }.getOrDefault("密钥格式不正确"); delay(1000) } }
    Column(modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(23.dp, 20.dp), verticalArrangement = Arrangement.spacedBy(19.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) { Field("账号 / 邮箱", note.username, Modifier.weight(1f).testTag("account")) { text -> vm.edit(note.id) { it.copy(username = text) } }; Tool(Icons.Outlined.ContentCopy, "复制账号") { activity.copy(note.username) } }
        Row(verticalAlignment = Alignment.CenterVertically) { Field("密码", note.password, Modifier.weight(1f).testTag("credential-password"), secret = true) { text -> vm.edit(note.id) { it.copy(password = text) } }; Tool(Icons.Outlined.ContentCopy, "复制密码") { activity.copy(note.password, true) }; Tool(Icons.Outlined.Casino, "生成强密码") { generator = true } }
        if (note.password.isNotBlank()) { val strength = Records.strength(note.password); LinearProgressIndicator(progress = { strength / 4f }, Modifier.fillMaxWidth(), color = if (strength < 2) MaterialTheme.colorScheme.secondary else MaterialTheme.colorScheme.primary); Text("密码强度：${listOf("较弱", "一般", "良好", "很强", "很强")[strength]}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
        Row(verticalAlignment = Alignment.CenterVertically) { Field("网站", note.url, Modifier.weight(1f)) { text -> vm.edit(note.id) { it.copy(url = text) } }; Tool(Icons.Outlined.OpenInNew, "打开网站") { activity.openUrl(note.url) } }
        Field("动态验证码密钥（TOTP，可选）", note.otp, secret = true) { text -> vm.edit(note.id) { it.copy(otp = text) } }
        if (otp.isNotBlank()) Surface(shape = RoundedCornerShape(13.dp), color = MaterialTheme.colorScheme.surfaceVariant) { Row(Modifier.fillMaxWidth().padding(15.dp), verticalAlignment = Alignment.CenterVertically) { Text(otp, fontSize = 26.sp, fontFamily = FontFamily.Monospace, modifier = Modifier.weight(1f)); Text("${remaining}s", style = MaterialTheme.typography.labelSmall); Tool(Icons.Outlined.ContentCopy, "复制动态验证码") { if (otp.all(Char::isDigit)) activity.copy(otp, true) } } }
        Field("备注", note.content, lines = 4) { text -> vm.edit(note.id) { it.copy(content = text) } }
    }
    if (generator) PasswordDialog(activity, { generator = false }) { password -> vm.edit(note.id) { it.copy(password = password) }; generator = false }
}
