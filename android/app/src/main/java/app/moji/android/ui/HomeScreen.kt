package app.moji.android.ui

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.grid.*
import androidx.compose.foundation.shape.RoundedCornerShape
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
@Composable fun HomeScreen(vm: MojiViewModel, s: AppState, newBook: () -> Unit, editBook: (Notebook) -> Unit) {
    val w = s.workspace ?: return
    BoxWithConstraints(Modifier.fillMaxSize()) {
        val compact = maxWidth < 600.dp
        val padding = if (compact) 22.dp else 38.dp
        LazyVerticalGrid(modifier = Modifier.widthIn(max = 1120.dp).fillMaxWidth().align(Alignment.TopCenter), columns = if(compact)GridCells.Fixed(2) else GridCells.Adaptive(180.dp), contentPadding = PaddingValues(padding), horizontalArrangement = Arrangement.spacedBy(16.dp), verticalArrangement = Arrangement.spacedBy(13.dp)) {
            item(span = { GridItemSpan(maxLineSpan) }) {
                Row(Modifier.fillMaxWidth().padding(top = 6.dp, bottom = 15.dp), verticalAlignment = Alignment.CenterVertically) {
                    Text("我的记事本  ${w.liveBooks.size}", fontSize = if (compact) 24.sp else 30.sp, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f).testTag("home-title"))

                }
            }
            items(w.liveBooks, key = { it.id }) { book -> NotebookCard(book, w.liveNotes.count { it.notebookId == book.id && !it.archived }, { vm.navigate(book.id) }, { editBook(book) }) }
            item(key="add-notebook"){AddNotebookCard(newBook)}
            item(span = { GridItemSpan(maxLineSpan) }) {
                Column(Modifier.padding(top = 14.dp)) {
                    HorizontalDivider(Modifier.padding(bottom = 23.dp), color = MaterialTheme.colorScheme.outlineVariant)
                    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                        Text("最近记录", fontSize = if (compact) 21.sp else 25.sp, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
                        TextButton(onClick = { vm.navigate("all") }) { Text("查看全部"); Icon(MojiIcons.ChevronRight, null, Modifier.size(17.dp)) }
                    }
                }
            }
            items(w.liveNotes.filter { !it.archived }.sortedByDescending { it.updatedAt }.take(6), key = { "recent-${it.id}" }, span = { GridItemSpan(maxLineSpan) }) { note ->
                val book = w.notebooks.first { it.id == note.notebookId }
                Surface(onClick = { vm.navigate(book.id); vm.select(note.id) }, color = MaterialTheme.colorScheme.surface, shape = RoundedCornerShape(12.dp), border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)) { Row(Modifier.fillMaxWidth().padding(horizontal = 15.dp, vertical = 13.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(13.dp)) {
                    Surface(shape = RoundedCornerShape(10.dp), color = modeColor(book.mode).copy(alpha = .08f)) { Icon(notebookIcon(book), null, Modifier.padding(10.dp).size(19.dp), tint = modeColor(book.mode)) }
                    Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(6.dp)) { Text(note.title,fontWeight = androidx.compose.ui.text.font.FontWeight.Bold, maxLines = 1, overflow = TextOverflow.Ellipsis, style = MaterialTheme.typography.bodyLarge); Text(book.name, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    if (note.favorite) Icon(MojiIcons.Star, null, Modifier.size(15.dp), tint = MaterialTheme.colorScheme.secondary)
                    Text(Records.relative(note.updatedAt), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                } }
            }

        }
    }
}

@OptIn(androidx.compose.foundation.ExperimentalFoundationApi::class)
@Composable fun NoteList(vm: MojiViewModel, s: AppState, modifier: Modifier = Modifier, editBook: (Notebook) -> Unit) {
    val w = s.workspace ?: return; val notes = vm.filtered()
    var selected by remember(s.view) { mutableStateOf(emptySet<String>()) }; var sortMenu by remember { mutableStateOf(false) }; var purge by remember { mutableStateOf(false) }
    Column(modifier.fillMaxHeight().background(MaterialTheme.colorScheme.surface)) {
        Column(Modifier.fillMaxWidth().padding(18.dp), verticalArrangement = Arrangement.spacedBy(11.dp)) {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Text(viewTitle(s), style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f))
                w.liveBooks.firstOrNull { it.id == s.view }?.let { Tool(MojiIcons.MoreHoriz, "记事本设置") { editBook(it) } }
            }
            OutlinedTextField(s.search, vm::search, Modifier.fillMaxWidth().testTag("list-search"), placeholder = { Text("搜索记录…", style = MaterialTheme.typography.bodyMedium) }, leadingIcon = { Icon(MojiIcons.Search, null, Modifier.size(18.dp)) }, trailingIcon = if (s.search.isNotBlank()) ({ Tool(MojiIcons.Close, "清除搜索") { vm.search("") } }) else null, singleLine = true, shape = RoundedCornerShape(12.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box { TextButton(onClick = { sortMenu = true }, contentPadding = PaddingValues(0.dp)) { Text(when (s.sort) { "name" -> "名称顺序"; "created" -> "创建时间"; else -> "最近编辑" }, style = MaterialTheme.typography.labelSmall); Icon(MojiIcons.ExpandMore, null, Modifier.size(16.dp)) }; DropdownMenu(sortMenu, { sortMenu = false }) { listOf("updated" to "最近编辑", "created" to "创建时间", "name" to "名称顺序").forEach { (id, title) -> DropdownMenuItem(text = { Text(title) }, onClick = { vm.sort(id); sortMenu = false }) } } }
                Spacer(Modifier.weight(1f)); Text("${notes.size}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                if (s.view == "trash") TextButton(onClick = { selected = w.notes.filter { it.deletedAt != null && it.purgedAt == null }.map { it.id }.toSet(); purge = true }, enabled = w.notes.any { it.deletedAt != null && it.purgedAt == null }, modifier = Modifier.testTag("empty-trash")) { Text("清空回收站") }
            }
        }
        if (selected.isNotEmpty()) Surface(color = MaterialTheme.colorScheme.primaryContainer) { Row(Modifier.fillMaxWidth().padding(horizontal = 12.dp), verticalAlignment = Alignment.CenterVertically) { Checkbox(selected.size == notes.size, { selected = if (selected.size == notes.size) emptySet() else notes.map { it.id }.toSet() }); Text("已选 ${selected.size}", Modifier.weight(1f), style = MaterialTheme.typography.labelSmall); if (s.view == "trash") Tool(MojiIcons.RestoreFromTrash, "恢复所选") { vm.restore(selected); selected = emptySet() } else Tool(MojiIcons.StarOutline, "收藏所选") { selected.forEach { id -> vm.edit(id) { it.copy(favorite = true) } }; selected = emptySet() }; Tool(MojiIcons.DeleteOutline, "删除所选") { if (s.view == "trash") purge = true else { vm.trash(selected); selected = emptySet() } }; Tool(MojiIcons.Close, "取消多选") { selected = emptySet() } } }
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        LazyColumn(Modifier.weight(1f).testTag("note-list"), contentPadding = PaddingValues(7.dp, 8.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
            items(notes, key = { it.id }) { note -> val mode = w.notebooks.first { it.id == note.notebookId }.mode
                Surface(shape = RoundedCornerShape(13.dp), color = if (s.selectedId == note.id || note.id in selected) MaterialTheme.colorScheme.primaryContainer.copy(alpha = .75f) else Color.Transparent, modifier = Modifier.fillMaxWidth().combinedClickable(onClick = { if (selected.isEmpty()) vm.select(note.id) else selected = if (note.id in selected) selected - note.id else selected + note.id }, onLongClick = { selected = selected + note.id })) {
                    Row(Modifier.padding(15.dp, 17.dp), verticalAlignment = Alignment.CenterVertically) {
                        if (selected.isNotEmpty()) Checkbox(note.id in selected, { selected = if (note.id in selected) selected - note.id else selected + note.id }, Modifier.padding(end = 6.dp))
                        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(11.dp)) {
                            Row(verticalAlignment = Alignment.CenterVertically) { Text(note.title.ifBlank { "未命名记录" }, Modifier.weight(1f), maxLines = 1, overflow = TextOverflow.Ellipsis, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold); if (note.pinned) Icon(MojiIcons.PushPin, null, Modifier.size(13.dp)); if (note.favorite) Icon(MojiIcons.Star, null, Modifier.size(13.dp), tint = MaterialTheme.colorScheme.secondary) }
                            Text(if (mode == NoteMode.Password) note.username else Records.plain(PromptVariables.display(note)).take(100), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2, overflow = TextOverflow.Ellipsis)
                            Row { Text(Records.relative(note.updatedAt), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                        }
                    }
                }
            }
            if (notes.isEmpty()) item { Column(Modifier.fillMaxWidth().padding(25.dp, 60.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(20.dp)) { Icon(MojiIcons.Article, null, Modifier.size(37.dp), tint = MaterialTheme.colorScheme.outline); Text(if (s.search.isNotBlank()) "没有找到相关记录" else if (s.view == "trash") "回收站是空的" else "暂无记录", color = MaterialTheme.colorScheme.onSurfaceVariant) } }
        }
    }
    if (purge) Confirm("永久删除 ${selected.size} 条记录？", "正文、密码和历史版本会被清除，无法从回收站恢复。", { purge = false }) { vm.purge(selected); selected = emptySet() }
}
fun viewTitle(s: AppState) = s.workspace?.liveBooks?.firstOrNull { it.id == s.view }?.name ?: when (s.view) { "home" -> "工作台"; "star" -> "我的收藏"; "archive" -> "归档记录"; "trash" -> "回收站"; else -> "全部记录" }
