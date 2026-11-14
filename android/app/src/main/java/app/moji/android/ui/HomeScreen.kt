package app.moji.android.ui

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.grid.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import app.moji.android.*
import app.moji.android.data.*

@OptIn(androidx.compose.foundation.ExperimentalFoundationApi::class)
@Composable fun HomeScreen(vm: MojiViewModel, s: AppState, newBook: () -> Unit, editBook: (Notebook) -> Unit, templates: () -> Unit) {
    val w = s.workspace ?: return
    BoxWithConstraints(Modifier.fillMaxSize()) {
        val compact = maxWidth < 600.dp
        val padding = if (compact) 22.dp else 38.dp
        LazyVerticalGrid(columns = if (compact) GridCells.Fixed(1) else GridCells.Adaptive(210.dp), contentPadding = PaddingValues(padding), horizontalArrangement = Arrangement.spacedBy(16.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
            item(span = { GridItemSpan(maxLineSpan) }) {
                Row(Modifier.fillMaxWidth().padding(top = 9.dp, bottom = if (compact) 18.dp else 28.dp), verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(15.dp)) {
                        Text("A LITTLE SPACE, ALL YOURS", fontSize = 9.sp, letterSpacing = 1.5.sp, color = MaterialTheme.colorScheme.primary)
                        Text("每一份记录，\n都有自己的位置。", fontSize = if (compact) 29.sp else 36.sp, lineHeight = if (compact) 43.sp else 54.sp, fontWeight = FontWeight.Medium, modifier = Modifier.testTag("home-title"))
                        Text("收藏灵感，整理日常。", color = MaterialTheme.colorScheme.onSurfaceVariant, style = MaterialTheme.typography.bodyMedium)
                    }
                    if (!compact) Surface(Modifier.size(132.dp), shape = RoundedCornerShape(28.dp), color = MaterialTheme.colorScheme.primaryContainer.copy(alpha = .55f)) { Box(contentAlignment = Alignment.Center) { Text("记", fontSize = 78.sp, fontWeight = FontWeight.Light, color = MaterialTheme.colorScheme.primary.copy(alpha = .7f)) } }
                }
            }
            item(span = { GridItemSpan(maxLineSpan) }) { Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) { Text("我的记事本", style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f)); TextButton(onClick = newBook) { Icon(Icons.Outlined.Add, null, Modifier.size(17.dp)); Text("新建") } } }
            items(w.liveBooks, key = { it.id }) { book ->
                val tint = modeColor(book.mode)
                Surface(Modifier.fillMaxWidth().combinedClickable(onClick = { vm.navigate(book.id) }, onLongClick = { editBook(book) }).testTag("book-${book.mode.name}"), shape = RoundedCornerShape(18.dp), color = tint.copy(alpha = .08f), border = androidx.compose.foundation.BorderStroke(1.dp, tint.copy(alpha = .09f))) {
                    if (compact) Row(Modifier.padding(19.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(15.dp)) {
                        Surface(shape = RoundedCornerShape(13.dp), color = tint.copy(alpha = .12f)) { Icon(modeIcon(book.mode), null, Modifier.padding(12.dp).size(25.dp), tint = tint) }
                        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(7.dp)) { Text(book.name, style = MaterialTheme.typography.titleMedium); Text("${book.mode.label} · ${w.liveNotes.count { it.notebookId == book.id && !it.archived }} 条记录", style = MaterialTheme.typography.labelSmall, color = tint) }
                        Tool(Icons.Outlined.MoreHoriz, "编辑记事本 ${book.name}") { editBook(book) }
                    } else Column(Modifier.padding(23.dp), verticalArrangement = Arrangement.spacedBy(22.dp)) {
                        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) { Icon(modeIcon(book.mode), null, Modifier.size(31.dp), tint = tint); Spacer(Modifier.weight(1f)); Tool(Icons.Outlined.MoreHoriz, "编辑记事本 ${book.name}") { editBook(book) } }
                        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) { Text(book.name, style = MaterialTheme.typography.titleMedium); Row { Text("${w.liveNotes.count { it.notebookId == book.id && !it.archived }} 条记录", style = MaterialTheme.typography.labelSmall, color = tint); Spacer(Modifier.weight(1f)); Icon(Icons.Outlined.NorthEast, null, Modifier.size(17.dp), tint = tint) } }
                    }
                }
            }
            item(span = { GridItemSpan(maxLineSpan) }) { OutlinedButton(onClick = templates, modifier = Modifier.fillMaxWidth().padding(top = 2.dp, bottom = 14.dp), shape = RoundedCornerShape(14.dp), border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)) { Icon(Icons.Outlined.AutoAwesome, null, Modifier.size(17.dp)); Spacer(Modifier.width(10.dp)); Text("从模板开始，记录新的想法") } }
            item(span = { GridItemSpan(maxLineSpan) }) { Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) { Text("最近记录", style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f)); TextButton(onClick = { vm.navigate("all") }) { Text("查看全部"); Icon(Icons.Outlined.ChevronRight, null, Modifier.size(17.dp)) } } }
            items(w.liveNotes.filter { !it.archived }.sortedByDescending { it.updatedAt }.take(6), key = { "recent-${it.id}" }, span = { GridItemSpan(maxLineSpan) }) { note ->
                val book = w.notebooks.first { it.id == note.notebookId }
                Surface(onClick = { vm.navigate(book.id); vm.select(note.id) }, color = Color.Transparent) { Row(Modifier.fillMaxWidth().padding(vertical = 9.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(13.dp)) {
                    Surface(shape = RoundedCornerShape(10.dp), color = modeColor(book.mode).copy(alpha = .08f)) { Icon(modeIcon(book.mode), null, Modifier.padding(10.dp).size(19.dp), tint = modeColor(book.mode)) }
                    Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(6.dp)) { Text(note.title, maxLines = 1, overflow = TextOverflow.Ellipsis, style = MaterialTheme.typography.bodyLarge); Text(book.name, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    if (note.favorite) Icon(Icons.Outlined.Star, null, Modifier.size(15.dp), tint = MaterialTheme.colorScheme.secondary)
                    Text(Records.relative(note.updatedAt), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                } }
            }
            item(span = { GridItemSpan(maxLineSpan) }) { Text("你的记录，只属于你。", Modifier.padding(vertical = 20.dp), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
        }
    }
}

@OptIn(androidx.compose.foundation.ExperimentalFoundationApi::class)
@Composable fun NoteList(vm: MojiViewModel, s: AppState, modifier: Modifier = Modifier, editBook: (Notebook) -> Unit, templates: () -> Unit) {
    val w = s.workspace ?: return; val notes = vm.filtered()
    var selected by remember(s.view) { mutableStateOf(emptySet<String>()) }; var sortMenu by remember { mutableStateOf(false) }; var purge by remember { mutableStateOf(false) }
    Column(modifier.fillMaxHeight().background(MaterialTheme.colorScheme.surface)) {
        Column(Modifier.fillMaxWidth().padding(18.dp), verticalArrangement = Arrangement.spacedBy(11.dp)) {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Text(viewTitle(s), style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f))
                w.liveBooks.firstOrNull { it.id == s.view }?.let { Tool(Icons.Outlined.MoreHoriz, "记事本设置") { editBook(it) } }
            }
            OutlinedTextField(s.search, vm::search, Modifier.fillMaxWidth().testTag("list-search"), placeholder = { Text("搜索记录…", style = MaterialTheme.typography.bodyMedium) }, leadingIcon = { Icon(Icons.Outlined.Search, null, Modifier.size(18.dp)) }, trailingIcon = if (s.search.isNotBlank()) ({ Tool(Icons.Outlined.Close, "清除搜索") { vm.search("") } }) else null, singleLine = true, shape = RoundedCornerShape(12.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box { TextButton(onClick = { sortMenu = true }, contentPadding = PaddingValues(0.dp)) { Text(when (s.sort) { "name" -> "名称顺序"; "created" -> "创建时间"; else -> "最近编辑" }, style = MaterialTheme.typography.labelSmall); Icon(Icons.Outlined.ExpandMore, null, Modifier.size(16.dp)) }; DropdownMenu(sortMenu, { sortMenu = false }) { listOf("updated" to "最近编辑", "created" to "创建时间", "name" to "名称顺序").forEach { (id, title) -> DropdownMenuItem(text = { Text(title) }, onClick = { vm.sort(id); sortMenu = false }) } } }
                Spacer(Modifier.weight(1f)); Text("${notes.size}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                if (s.view != "trash") Tool(Icons.Outlined.Add, "从模板新建", action = templates) else Tool(Icons.Outlined.DeleteSweep, "清空回收站", notes.isNotEmpty()) { selected = notes.map { it.id }.toSet(); purge = true }
            }
        }
        if (selected.isNotEmpty()) Surface(color = MaterialTheme.colorScheme.primaryContainer) { Row(Modifier.fillMaxWidth().padding(horizontal = 12.dp), verticalAlignment = Alignment.CenterVertically) { Checkbox(selected.size == notes.size, { selected = if (selected.size == notes.size) emptySet() else notes.map { it.id }.toSet() }); Text("已选 ${selected.size}", Modifier.weight(1f), style = MaterialTheme.typography.labelSmall); if (s.view == "trash") Tool(Icons.Outlined.RestoreFromTrash, "恢复所选") { vm.restore(selected); selected = emptySet() } else Tool(Icons.Outlined.StarOutline, "收藏所选") { selected.forEach { id -> vm.edit(id) { it.copy(favorite = true) } }; selected = emptySet() }; Tool(Icons.Outlined.DeleteOutline, "删除所选") { if (s.view == "trash") purge = true else { vm.trash(selected); selected = emptySet() } }; Tool(Icons.Outlined.Close, "取消多选") { selected = emptySet() } } }
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        LazyColumn(Modifier.weight(1f).testTag("note-list"), contentPadding = PaddingValues(7.dp, 8.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
            items(notes, key = { it.id }) { note -> val mode = w.notebooks.first { it.id == note.notebookId }.mode
                Surface(shape = RoundedCornerShape(13.dp), color = if (s.selectedId == note.id || note.id in selected) MaterialTheme.colorScheme.primaryContainer.copy(alpha = .75f) else Color.Transparent, modifier = Modifier.fillMaxWidth().combinedClickable(onClick = { if (selected.isEmpty()) vm.select(note.id) else selected = if (note.id in selected) selected - note.id else selected + note.id }, onLongClick = { selected = selected + note.id })) {
                    Row(Modifier.padding(15.dp, 17.dp), verticalAlignment = Alignment.CenterVertically) {
                        if (selected.isNotEmpty()) Checkbox(note.id in selected, { selected = if (note.id in selected) selected - note.id else selected + note.id }, Modifier.padding(end = 6.dp))
                        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(11.dp)) {
                            Row(verticalAlignment = Alignment.CenterVertically) { Text(note.title.ifBlank { "未命名记录" }, Modifier.weight(1f), maxLines = 1, overflow = TextOverflow.Ellipsis, fontWeight = FontWeight.Medium); if (note.pinned) Icon(Icons.Outlined.PushPin, null, Modifier.size(13.dp)); if (note.favorite) Icon(Icons.Outlined.Star, null, Modifier.size(13.dp), tint = MaterialTheme.colorScheme.secondary) }
                            Text(if (mode == NoteMode.Password) note.username.ifBlank { "尚未填写账号" } else Records.plain(note.content).take(100).ifBlank { "还没有内容" }, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2, overflow = TextOverflow.Ellipsis)
                            Row { Text(Records.relative(note.updatedAt), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant); Spacer(Modifier.weight(1f)); note.tags.firstOrNull()?.let { Text("#$it", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.primary) } }
                        }
                    }
                }
            }
            if (notes.isEmpty()) item { Column(Modifier.fillMaxWidth().padding(25.dp, 60.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(20.dp)) { Icon(Icons.Outlined.Article, null, Modifier.size(37.dp), tint = MaterialTheme.colorScheme.outline); Text(if (s.search.isNotBlank()) "没有找到相关记录" else if (s.view == "trash") "回收站是空的" else "这里，留给新的记录。", color = MaterialTheme.colorScheme.onSurfaceVariant); if (s.view != "trash" && s.search.isBlank()) TextButton(onClick = { vm.addNote() }) { Text("写下第一条") } } }
        }
    }
    if (purge) Confirm("永久删除 ${selected.size} 条记录？", "正文、密码和历史版本会被清除，无法从回收站恢复。", { purge = false }) { vm.purge(selected); selected = emptySet() }
}
fun viewTitle(s: AppState) = s.workspace?.liveBooks?.firstOrNull { it.id == s.view }?.name ?: when (s.view) { "home" -> "我的工作台"; "star" -> "我的收藏"; "archive" -> "归档记录"; "trash" -> "回收站"; else -> "全部记录" }
