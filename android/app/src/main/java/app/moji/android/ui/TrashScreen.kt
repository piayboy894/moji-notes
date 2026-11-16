package app.moji.android.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.Alignment
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import app.moji.android.*
import app.moji.android.data.*

@OptIn(ExperimentalFoundationApi::class)
@Composable fun TrashScreen(vm:MojiViewModel,s:AppState){
    val w=s.workspace?:return
    val all=w.notes.filter{it.deletedAt!=null&&it.purgedAt==null}
    var selected by remember{mutableStateOf(emptySet<String>())};var multi by remember{mutableStateOf(false)}
    var pending by remember{mutableStateOf(emptySet<String>())};var clearing by remember{mutableStateOf(false)};var sortMenu by remember{mutableStateOf(false)};var nameSort by remember{mutableStateOf(false)}
    val notes=all.filter{note->Records.matches(note,s.search,w.notebooks.first{it.id==note.notebookId}.mode)}.let{if(nameSort)it.sortedBy{n->n.title}else it.sortedByDescending{n->n.deletedAt}}
    LaunchedEffect(all.map{it.id}){selected=selected.intersect(all.map{it.id}.toSet())}
    BackHandler(selected.isNotEmpty()){selected=emptySet();multi=false}
    fun toggle(id:String){multi=true;selected=if(id in selected)selected-id else selected+id}
    Column(Modifier.fillMaxSize().testTag("trash-page")){
        Column(Modifier.padding(horizontal=20.dp,vertical=16.dp),verticalArrangement=Arrangement.spacedBy(12.dp)){
            Row(Modifier.fillMaxWidth(),verticalAlignment=Alignment.CenterVertically){Text("已删除的记录  ${all.size}",Modifier.weight(1f),style=MaterialTheme.typography.titleLarge);TextButton(onClick={pending=all.map{it.id}.toSet();clearing=true},enabled=all.isNotEmpty(),modifier=Modifier.testTag("empty-trash")){Text("清空回收站",color=if(all.isEmpty())MaterialTheme.colorScheme.onSurfaceVariant else MaterialTheme.colorScheme.error)}}
            OutlinedTextField(s.search,vm::search,Modifier.fillMaxWidth().testTag("list-search"),singleLine=true,placeholder={Text("搜索回收站")},leadingIcon={Icon(MojiIcons.Search,null,Modifier.size(18.dp))},shape=RoundedCornerShape(11.dp))
            Row(Modifier.fillMaxWidth(),verticalAlignment=Alignment.CenterVertically){Text(if(selected.isEmpty())"${notes.size} 项"else"已选 ${selected.size} 项",style=MaterialTheme.typography.labelMedium,modifier=Modifier.weight(1f));TextButton(onClick={selected=notes.map{it.id}.toSet();multi=true},enabled=notes.isNotEmpty()){Text("全选")};Box{Tool(MojiIcons.ExpandMore,"排序"){sortMenu=true};DropdownMenu(sortMenu,{sortMenu=false}){DropdownMenuItem(text={Text("最近删除")},onClick={nameSort=false;sortMenu=false});DropdownMenuItem(text={Text("名称顺序")},onClick={nameSort=true;sortMenu=false})}}}
            Row(horizontalArrangement=Arrangement.spacedBy(10.dp),verticalAlignment=Alignment.CenterVertically){Button(onClick={vm.restore(selected);selected=emptySet();multi=false},enabled=selected.isNotEmpty(),modifier=Modifier.testTag("restore-trash")){Text("还原")};OutlinedButton(onClick={pending=selected;clearing=false},enabled=selected.isNotEmpty(),modifier=Modifier.testTag("delete-trash")){Text("删除")};if(selected.isNotEmpty())Tool(MojiIcons.Close,"取消选择"){selected=emptySet();multi=false}}
        }
        LazyVerticalGrid(columns=GridCells.Adaptive(240.dp),modifier=Modifier.weight(1f).testTag("trash-grid"),contentPadding=PaddingValues(20.dp,0.dp,20.dp,24.dp),horizontalArrangement=Arrangement.spacedBy(12.dp),verticalArrangement=Arrangement.spacedBy(12.dp)){
            items(notes,key={it.id}){note->val book=w.notebooks.first{it.id==note.notebookId};val chosen=note.id in selected
                Surface(shape=RoundedCornerShape(13.dp),color=if(chosen)MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surface,border=BorderStroke(1.dp,if(chosen)MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant),modifier=Modifier.fillMaxWidth().height(184.dp).combinedClickable(onClick={if(multi)toggle(note.id)else selected=setOf(note.id)},onLongClick={toggle(note.id)}).testTag("trash-record-${note.id}")){
                    Column(Modifier.padding(16.dp)){
                        Row(verticalAlignment=Alignment.CenterVertically){Icon(notebookIcon(book),null,Modifier.size(21.dp),tint=color(book.color));Spacer(Modifier.width(8.dp));Text(book.name,Modifier.weight(1f),style=MaterialTheme.typography.labelSmall,maxLines=1,overflow=TextOverflow.Ellipsis,color=MaterialTheme.colorScheme.onSurfaceVariant);Checkbox(chosen,{toggle(note.id)},Modifier.size(26.dp).testTag("trash-select-${note.id}"))}
                        Spacer(Modifier.height(13.dp));Text(note.title,style=MaterialTheme.typography.titleMedium,fontWeight=FontWeight.SemiBold,maxLines=2,overflow=TextOverflow.Ellipsis)
                        Spacer(Modifier.height(7.dp));Text(if(book.mode==NoteMode.Password)note.username else Records.plain(PromptVariables.display(note)).take(110),style=MaterialTheme.typography.bodyMedium,maxLines=2,overflow=TextOverflow.Ellipsis,color=MaterialTheme.colorScheme.onSurfaceVariant)
                        Spacer(Modifier.weight(1f));Text(java.text.SimpleDateFormat("MM-dd  HH:mm",java.util.Locale.getDefault()).format(java.util.Date(note.deletedAt!!)),style=MaterialTheme.typography.labelSmall,color=MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
            if(notes.isEmpty())item(span={GridItemSpan(maxLineSpan)}){Column(Modifier.fillMaxWidth().padding(vertical=60.dp),horizontalAlignment=Alignment.CenterHorizontally,verticalArrangement=Arrangement.spacedBy(15.dp)){Icon(MojiIcons.DeleteOutline,null,Modifier.size(38.dp),tint=MaterialTheme.colorScheme.outline);Text(if(s.search.isBlank())"回收站是空的"else"没有找到相关记录",color=MaterialTheme.colorScheme.onSurfaceVariant)}}
        }
    }
    if(pending.isNotEmpty())Confirm(if(clearing)"清空回收站？"else"删除 ${pending.size} 条记录？","正文、密码和历史版本会被清除，无法恢复。",{pending=emptySet()}){vm.purge(pending);pending=emptySet();selected=emptySet();multi=false}
}
