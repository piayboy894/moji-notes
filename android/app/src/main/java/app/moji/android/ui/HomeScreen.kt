package app.moji.android.ui

import androidx.activity.compose.BackHandler
import androidx.compose.animation.core.*
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.foundation.focusable
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.focus.onFocusChanged
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
import sh.calvin.reorderable.*
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
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
    val gridState=rememberLazyGridState();val haptic=LocalHapticFeedback.current
    val (preview,dragInteractions)=rememberNotebookReorderPreview(w.id,w.liveBooks.map{it.id},vm::setBookOrder)
    val booksById=w.liveBooks.associateBy{it.id}
    val reordering=rememberReorderableLazyGridState(gridState){from,to->preview.move(from.key.toString(),to.key.toString())}
    BoxWithConstraints(Modifier.fillMaxSize()) {
        val compact = maxWidth < 600.dp
        val padding = if (compact) 22.dp else 38.dp
        LazyVerticalGrid(state=gridState,modifier = Modifier.widthIn(max = 1120.dp).fillMaxWidth().align(Alignment.TopCenter), columns = if(compact)GridCells.Fixed(2) else GridCells.Adaptive(180.dp), contentPadding = PaddingValues(padding), horizontalArrangement = Arrangement.spacedBy(16.dp), verticalArrangement = Arrangement.spacedBy(13.dp)) {
            item(span = { GridItemSpan(maxLineSpan) }) {
                Row(Modifier.fillMaxWidth().padding(top = 6.dp, bottom = 15.dp), verticalAlignment = Alignment.CenterVertically) {
                    Text("我的记事本  ${w.liveBooks.size}", fontSize = if (compact) 24.sp else 30.sp, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f).testTag("home-title"))

                }
            }
            items(preview.order.mapNotNull{booksById[it]}, key = { it.id }) { book -> ReorderableItem(reordering,key=book.id,animateItemModifier=Modifier.animateItem(fadeInSpec=null,placementSpec=tween(150),fadeOutSpec=null)){dragging->
                NotebookCard(book,w.liveNotes.count{it.notebookId==book.id&&!it.archived},{vm.navigate(book.id)},{editBook(book)},Modifier.longPressDraggableHandle(interactionSource=dragInteractions,onDragStarted={preview.begin();haptic.performHapticFeedback(HapticFeedbackType.LongPress)}))
            } }
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
@Composable fun NoteList(vm: MojiViewModel, s: AppState, modifier: Modifier = Modifier, editBook: (Notebook) -> Unit, overview:Boolean=false, sharedMotion:RecordSharedMotion?=null) {
    val w = s.workspace ?: return; val notes = vm.filtered()
    val recordListFocus=remember{FocusRequester()}
    var selected by remember(s.view,s.selectedId) { RecordCommandState(emptySet<String>()){vm.recordSelection=it} }; var selecting by remember(s.view,s.selectedId){RecordCommandState(false){vm.recordSelectionMode=it}}; var sortMenu by remember { mutableStateOf(false) }; var purge by remember { mutableStateOf(false) }
    val selectedIndex=notes.indexOfFirst{it.id==s.selectedId}.coerceAtLeast(0)
    // Overview scroll is saved per notebook. Detail starts at the opened record,
    // never at a previous record's saved offset or at the top of a long list.
    val recordGridState=if(overview)rememberLazyGridState()else remember(s.view){LazyGridState(firstVisibleItemIndex=selectedIndex)}
    LaunchedEffect(s.selectedId,overview){if(!overview&&s.selectedId!=null){val at=notes.indexOfFirst{it.id==s.selectedId};if(at>=0&&recordGridState.layoutInfo.visibleItemsInfo.none{it.key==s.selectedId})recordGridState.scrollToItem(at)}}
    BackHandler(selecting){selected=emptySet();selecting=false}
    val visibleIds=notes.map{it.id}.toSet();LaunchedEffect(visibleIds){selected=selected.intersect(visibleIds)}
    Box(modifier.fillMaxHeight().focusRequester(recordListFocus).focusable()) {
    Box(Modifier.matchParentSize().background(MaterialTheme.colorScheme.surfaceContainerLow))
    Column(Modifier.fillMaxSize()) {
        Column(Modifier.fillMaxWidth().padding(18.dp), verticalArrangement = Arrangement.spacedBy(11.dp)) {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.weight(1f)){Text(viewTitle(s), style = MaterialTheme.typography.titleMedium, maxLines=1,overflow=TextOverflow.Ellipsis)}
                if(s.recordClipboardReady&&vm.hasRecordClipboard()&&w.liveBooks.any{it.id==s.view})TextButton(onClick={vm.pasteRecords()},modifier=Modifier.testTag("paste-records")){Text("粘贴")}

            }
            OutlinedTextField(s.search, vm::search, Modifier.fillMaxWidth().onFocusChanged{if(it.hasFocus)vm.recordKeyboardScope=false}.testTag("list-search"), placeholder = { Text("搜索记录…", style = MaterialTheme.typography.bodyMedium) }, leadingIcon = { Icon(MojiIcons.Search, null, Modifier.size(18.dp)) }, trailingIcon = if (s.search.isNotBlank()) ({ Tool(MojiIcons.Close, "清除搜索") { vm.search("") } }) else null, singleLine = true, shape = RoundedCornerShape(12.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box { TextButton(onClick = { sortMenu = true }, contentPadding = PaddingValues(0.dp)) { Text(when (s.sort) { "name" -> "名称顺序"; "created" -> "创建时间"; else -> "最近编辑" }, style = MaterialTheme.typography.labelSmall); Icon(MojiIcons.ExpandMore, null, Modifier.size(16.dp)) }; DropdownMenu(sortMenu, { sortMenu = false }) { listOf("updated" to "最近编辑", "created" to "创建时间", "name" to "名称顺序").forEach { (id, title) -> DropdownMenuItem(text = { Text(title) }, onClick = { vm.sort(id); sortMenu = false }) } } }
                Spacer(Modifier.weight(1f));if(!selecting)TextButton(onClick={recordListFocus.requestFocus();vm.recordKeyboardScope=true;selecting=true;selected=emptySet()},modifier=Modifier.testTag("select-records")){Text("选择")}
                if (s.view == "trash") TextButton(onClick = { selected = w.notes.filter { it.deletedAt != null && it.purgedAt == null }.map { it.id }.toSet(); purge = true }, enabled = w.notes.any { it.deletedAt != null && it.purgedAt == null }, modifier = Modifier.testTag("empty-trash")) { Text("清空回收站") }
            }
        }
        if(selecting)Surface(color=MaterialTheme.colorScheme.primaryContainer){Column(Modifier.fillMaxWidth().padding(horizontal=12.dp)){
            Row(verticalAlignment=Alignment.CenterVertically){Text("已选 ${selected.size}",Modifier.weight(1f),style=MaterialTheme.typography.labelSmall)
                TextButton(onClick={recordListFocus.requestFocus();vm.recordKeyboardScope=true;selected=if(selected.size==notes.size)emptySet()else notes.map{it.id}.toSet()},modifier=Modifier.testTag("select-all-records")){Text(if(selected.isNotEmpty()&&selected.size==notes.size)"取消全选"else"全选")}
                TextButton(onClick={selected=emptySet();selecting=false},modifier=Modifier.testTag("cancel-record-selection")){Text("取消")}
            }
            Row(verticalAlignment=Alignment.CenterVertically){
                TextButton(onClick={vm.copyRecords(selected,false)},enabled=selected.isNotEmpty(),modifier=Modifier.testTag("copy-records")){Text("复制")}
                TextButton(onClick={vm.copyRecords(selected,true)},enabled=selected.isNotEmpty(),modifier=Modifier.testTag("cut-records")){Text("剪切")}
                Spacer(Modifier.weight(1f))
                TextButton(onClick={if(s.view=="trash")purge=true else{vm.trash(selected);selected=emptySet();selecting=false}},enabled=selected.isNotEmpty(),modifier=Modifier.testTag("delete-selected-records")){Text("删除",color=if(selected.isNotEmpty())MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant)}
            }
        }}
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        LazyVerticalGrid(columns=if(overview)GridCells.Adaptive(240.dp)else GridCells.Fixed(1),state=recordGridState,modifier=Modifier.weight(1f).testTag("note-list"), contentPadding = PaddingValues(if(overview)18.dp else 7.dp,8.dp), horizontalArrangement=Arrangement.spacedBy(12.dp),verticalArrangement = Arrangement.spacedBy(9.dp)) {
            items(notes, key = { it.id },contentType={"record"}) { note -> var contextMenu by remember(note.id){mutableStateOf(false)}
                val clickCard:()->Unit={recordListFocus.requestFocus();vm.recordKeyboardScope=true;if(!selecting)vm.select(note.id)else selected=if(note.id in selected)selected-note.id else selected+note.id}
                val gestures=if(selecting&&note.id in selected)Modifier.recordDragSource(vm,selected,clickCard){note.id in selected}else Modifier.combinedClickable(onClick=clickCard,onLongClick={recordListFocus.requestFocus();vm.recordKeyboardScope=true;selecting=true;selected=selected+note.id})
                val mode = w.notebooks.first { it.id == note.notebookId }.mode;val chosen=if(selecting)note.id in selected else s.selectedId==note.id
                val preview=remember(note.content,note.variables,note.username,mode){if(mode==NoteMode.Password)note.username else Records.plain(PromptVariables.display(note)).take(100)}
                Surface(shape = RoundedCornerShape(13.dp), color = if (chosen) color(LocalMojiPalette.current.colors.getValue("RecordSelected")) else MaterialTheme.colorScheme.surface, contentColor=if(chosen)color(LocalMojiPalette.current.colors.getValue("RecordSelectedInk"))else MaterialTheme.colorScheme.onSurface,border=androidx.compose.foundation.BorderStroke(1.dp,if(chosen)color(LocalMojiPalette.current.colors.getValue("RecordSelected"))else MaterialTheme.colorScheme.outlineVariant), modifier = Modifier.fillMaxWidth().recordSharedBounds("card-${note.id}",sharedMotion,enabled=note.id==s.selectedId,z=if(note.id==s.selectedId)3f else 1f).then(if(overview)Modifier.height(190.dp)else Modifier).testTag("record-card-${note.id}").recordContextTrigger{contextMenu=true;recordListFocus.requestFocus();vm.recordKeyboardScope=true}.then(gestures)) {
                    DropdownMenu(contextMenu,{contextMenu=false}){
                        val ids=if(selecting&&note.id in selected)selected else setOf(note.id)
                        DropdownMenuItem(text={Text("复制记录")},onClick={vm.copyRecords(ids,false);contextMenu=false})
                        DropdownMenuItem(text={Text("剪切记录")},onClick={vm.copyRecords(ids,true);contextMenu=false})
                        DropdownMenuItem(text={Text("粘贴记录")},enabled=s.recordClipboardReady&&w.liveBooks.any{it.id==s.view},onClick={vm.pasteRecords();contextMenu=false})
                        DropdownMenuItem(text={Text("创建副本")},enabled=ids.size==1,onClick={vm.duplicate(note);contextMenu=false})
                    }
                    Row(Modifier.padding(15.dp, 17.dp), verticalAlignment = Alignment.CenterVertically) {
                        if (selecting) Checkbox(note.id in selected, { vm.recordKeyboardScope=true;selected = if (note.id in selected) selected - note.id else selected + note.id }, Modifier.padding(end = 6.dp).testTag("select-record-${note.id}"),colors=CheckboxDefaults.colors(checkedColor=color(LocalMojiPalette.current.colors.getValue("RecordSelectedInk")),checkmarkColor=color(LocalMojiPalette.current.colors.getValue("RecordSelected"))))
                        Column(Modifier.weight(1f).then(if(overview)Modifier.fillMaxHeight()else Modifier), verticalArrangement = Arrangement.spacedBy(11.dp)) {
                            Row(verticalAlignment = Alignment.CenterVertically) { Text(note.title.ifBlank { "未命名记录" }, Modifier.weight(1f), maxLines = 1, overflow = TextOverflow.Ellipsis, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold); if (note.favorite) Icon(MojiIcons.Star, null, Modifier.size(13.dp), tint = MaterialTheme.colorScheme.secondary) }
                            Text(preview, style = MaterialTheme.typography.bodyMedium, color = if(chosen)color(LocalMojiPalette.current.colors.getValue("RecordSelectedSub"))else MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2, overflow = TextOverflow.Ellipsis)
                            if(overview)Spacer(Modifier.weight(1f));Row(Modifier.fillMaxWidth(),verticalAlignment=Alignment.CenterVertically){Text(Records.relative(note.updatedAt),Modifier.weight(1f),style=MaterialTheme.typography.labelSmall,color=if(chosen)color(LocalMojiPalette.current.colors.getValue("RecordSelectedSub"))else MaterialTheme.colorScheme.onSurfaceVariant);if(!selecting){IconToggleButton(checked=note.pinned,onCheckedChange={vm.togglePin(note.id)},modifier=Modifier.size(34.dp).testTag("pin-record-${note.id}"),colors=IconButtonDefaults.iconToggleButtonColors(contentColor=if(chosen)color(LocalMojiPalette.current.colors.getValue("RecordSelectedInk"))else MaterialTheme.colorScheme.onSurfaceVariant,checkedContentColor=if(chosen)color(LocalMojiPalette.current.colors.getValue("RecordSelected"))else MaterialTheme.colorScheme.onPrimary,checkedContainerColor=if(chosen)color(LocalMojiPalette.current.colors.getValue("RecordSelectedInk"))else MaterialTheme.colorScheme.primary)){Icon(if(note.pinned)MojiIcons.PushPinFilled else MojiIcons.PushPin,if(note.pinned)"取消置顶"else"置顶记录",Modifier.size(17.dp))}}}
                        }
                    }
                }
            }
            if (notes.isEmpty()) item { Column(Modifier.fillMaxWidth().padding(25.dp, 60.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(20.dp)) { Icon(MojiIcons.Article, null, Modifier.size(37.dp), tint = MaterialTheme.colorScheme.outline); Text(if (s.search.isNotBlank()) "没有找到相关记录" else if (s.view == "trash") "回收站是空的" else "暂无记录", color = MaterialTheme.colorScheme.onSurfaceVariant) } }
        }
    }
    }
    if (purge) Confirm("永久删除 ${selected.size} 条记录？", "正文、密码和历史版本会被清除，无法从回收站恢复。", { purge = false }) { vm.purge(selected); selected = emptySet();selecting=false }
}
fun viewTitle(s: AppState) = s.workspace?.liveBooks?.firstOrNull { it.id == s.view }?.name ?: when (s.view) { "home" -> "工作台"; "star" -> "我的收藏"; "archive" -> "归档记录"; "trash" -> "回收站"; else -> "全部记录" }
