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
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.foundation.focusGroup
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.Alignment
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.graphicsLayer
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
import kotlinx.coroutines.delay

@Composable fun EditorScreen(vm: MojiViewModel, s: AppState, note: Note, activity: MainActivity, export: (Note) -> Unit, modifier: Modifier = Modifier, showBack: Boolean = false, back: () -> Unit = {}) {
    val entry=remember{androidx.compose.animation.core.Animatable(0f)};LaunchedEffect(Unit){entry.animateTo(1f,androidx.compose.animation.core.tween(170))}
    val workspace=s.workspace?:return;val book=workspace.notebooks.first{it.id==note.notebookId};val editing=s.editingCredentialId==note.id
    var more by remember(note.id){mutableStateOf(false)};var history by remember(note.id){mutableStateOf(false)};var move by remember(note.id){mutableStateOf(false)};var purge by remember(note.id){mutableStateOf(false)}
    val credentialFocus=remember { androidx.compose.ui.focus.FocusRequester() }
    val titleEpoch=vm.workspaceEpoch
    fun focusContent(){if(vm.workspaceEpoch!=titleEpoch||vm.state.value.selectedId!=note.id)return;if(book.mode==NoteMode.Password)credentialFocus.requestFocus() else activity.editorSession.command("focus")}
    Column(modifier.graphicsLayer{alpha=.4f+.6f*entry.value;translationY=(1f-entry.value)*8.dp.toPx()}.fillMaxSize().onFocusChanged{if(it.hasFocus)vm.recordKeyboardScope=false}.focusGroup().background(MaterialTheme.colorScheme.surface)){
        Row(Modifier.fillMaxWidth().padding(horizontal=12.dp,vertical=4.dp),verticalAlignment=Alignment.CenterVertically){
            if(showBack)Tool(MojiIcons.ArrowBack,"返回记录",action=back)
            if(!showBack)TextButton(onClick=vm::clearSelection,modifier=Modifier.weight(1f).testTag("back-to-overview")){Icon(MojiIcons.ArrowBack,"返回卡片网格",Modifier.size(18.dp));Spacer(Modifier.width(6.dp));Text(book.name,Modifier.weight(1f),maxLines=1,overflow=TextOverflow.Ellipsis,style=MaterialTheme.typography.labelSmall,color=color(book.color))}else Text(book.name,Modifier.weight(1f),maxLines=1,overflow=TextOverflow.Ellipsis,style=MaterialTheme.typography.labelSmall,color=color(book.color))
            if(note.deletedAt==null){
                if(book.mode==NoteMode.Password)Button(onClick={vm.editCredential(!editing)},modifier=Modifier.testTag("edit-credential"),contentPadding=PaddingValues(14.dp,8.dp)){Text(if(editing)"完成" else "编辑密码")}
                Tool(if(note.favorite)MojiIcons.Star else MojiIcons.StarOutline,"收藏记录"){vm.edit(note.id){it.copy(favorite=!it.favorite)}}
                Tool(MojiIcons.History,"版本历史"){history=true}
            }
            Box{Tool(MojiIcons.MoreHoriz,"更多操作"){more=true};DropdownMenu(more,{more=false}){
                if(note.deletedAt==null){
                    DropdownMenuItem(text={Text(if(note.pinned)"取消置顶" else "置顶记录")},onClick={vm.togglePin(note.id);more=false})
                    DropdownMenuItem(text={Text("创建副本")},onClick={vm.duplicate(note);more=false})
                    DropdownMenuItem(text={Text(if(note.archived)"取消归档" else "归档记录")},onClick={vm.edit(note.id){it.copy(archived=!it.archived)};vm.clearSelection();more=false})
                    DropdownMenuItem(text={Text("移动到记事本")},onClick={move=true;more=false})
                    if(book.mode!=NoteMode.Password){DropdownMenuItem(text={Text("复制全文")},onClick={activity.copy(visibleText(activity,note));more=false});DropdownMenuItem(text={Text("导出")},onClick={export(note);more=false})}
                    DropdownMenuItem(text={Text("移到回收站")},onClick={vm.trash(setOf(note.id));more=false})
                }else DropdownMenuItem(text={Text("永久删除")},onClick={purge=true;more=false})
            }}
        }
        key(note.id,titleEpoch) { BasicTextField(note.title,{text->if(vm.workspaceEpoch==titleEpoch)vm.edit(note.id){it.copy(title=text.replace("\n"," ").replace("\r"," "))}},Modifier.fillMaxWidth().padding(23.dp,18.dp).testTag("note-title").onFocusChanged{if(it.isFocused)activity.editorSession.clearSelectionAvailability()}.onPreviewKeyEvent { e -> if(e.type==KeyEventType.KeyDown && (e.key==Key.Enter||e.key==Key.Tab)&&!e.isShiftPressed&&!e.isCtrlPressed&&!e.isAltPressed){focusContent();true}else false },singleLine=true,keyboardOptions=KeyboardOptions(imeAction=ImeAction.Next),keyboardActions=androidx.compose.foundation.text.KeyboardActions(onNext={focusContent()}),textStyle=MaterialTheme.typography.titleLarge.copy(color=MaterialTheme.colorScheme.onSurface),cursorBrush=SolidColor(MaterialTheme.colorScheme.primary),readOnly=note.deletedAt!=null||(book.mode==NoteMode.Password&&!editing)) }
        if(note.deletedAt!=null)Column(Modifier.padding(24.dp),verticalArrangement=Arrangement.spacedBy(16.dp)){Button(onClick={vm.restore(setOf(note.id))}){Text("恢复记录")};TextButton(onClick={purge=true}){Text("永久删除")}}
        else if(book.mode==NoteMode.Password)CredentialEditor(vm,note,activity,editing,credentialFocus,Modifier.weight(1f))
        else RichNoteEditor(note,book.mode,vm,Modifier.weight(1f))
    }
    if(history)HistoryDialog(note,{history=false},allowRestore=book.mode!=NoteMode.Password||editing){vm.restoreVersion(note.id,it);history=false}
    if(move)PanelDialog("移动到记事本",{move=false}){val books=workspace.liveBooks.filter{it.mode==book.mode&&it.id!=book.id};if(books.isEmpty())Text("暂无同类型记事本");books.forEach{target->TextButton(onClick={vm.edit(note.id){it.copy(notebookId=target.id)};vm.navigate(target.id);vm.select(note.id);move=false}){Text(target.name)}}}
    if(purge)Confirm("永久删除这条记录？","正文、密码和历史版本会被清除，无法恢复。",{purge=false}){vm.purge(setOf(note.id))}
}
@Composable fun HistoryPreview(content:String,size:Float,modifier:Modifier=Modifier) {
    androidx.compose.foundation.text.selection.SelectionContainer(modifier.verticalScroll(rememberScrollState()).padding(16.dp)) { Text(EditorContent.plain(content),fontSize=size.sp) }
}
@Composable private fun CredentialEditor(vm: MojiViewModel, note: Note, activity: MainActivity, editing: Boolean, credentialFocus:androidx.compose.ui.focus.FocusRequester, modifier: Modifier = Modifier) {
    var generator by remember { mutableStateOf(false) };var newField by remember(note.id){mutableStateOf("")};var removeField by remember(note.id){mutableStateOf<CredentialField?>(null)}
    Column(modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(23.dp, 20.dp), verticalArrangement = Arrangement.spacedBy(19.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) { Field("账号 / 邮箱", note.username, Modifier.weight(1f).then(Modifier.focusRequester(credentialFocus)).testTag("account"), readOnly = !editing) { text -> vm.edit(note.id) { it.copy(username = text) } }; Tool(MojiIcons.ContentCopy, "复制账号") { activity.copy(note.username) } }
        Row(verticalAlignment = Alignment.CenterVertically) { Field("密码", note.password, Modifier.weight(1f).testTag("credential-password"), sensitive = true, readOnly = !editing) { text -> vm.edit(note.id) { it.copy(password = text) } }; Tool(MojiIcons.ContentCopy, "复制密码") { activity.copy(note.password, true) }; if (editing) Tool(MojiIcons.Casino, "生成强密码") { generator = true } }
        Row(verticalAlignment = Alignment.CenterVertically) { Field("网站", note.url, Modifier.weight(1f), readOnly = !editing) { text -> vm.edit(note.id) { it.copy(url = text) } }; Tool(MojiIcons.OpenInNew, "打开网站") { activity.openUrl(note.url) } }
        note.fields.forEach{field->key(field.id){Row(verticalAlignment=Alignment.CenterVertically){Field(field.name,field.value,Modifier.weight(1f).testTag("credential-field-${field.id}"),readOnly=!editing){value->vm.edit(note.id){it.copy(fields=it.fields.map{f->if(f.id==field.id)f.copy(value=value)else f})}};Tool(MojiIcons.ContentCopy,"复制"+field.name,enabled=field.value.isNotEmpty()){activity.copy(field.value)};if(editing)Tool(MojiIcons.DeleteOutline,"删除"+field.name){if(field.value.isBlank())vm.edit(note.id){it.copy(fields=it.fields.filter{f->f.id!=field.id})}else removeField=field}}}}
        if(editing)Row(verticalAlignment=Alignment.CenterVertically,horizontalArrangement=Arrangement.spacedBy(8.dp)){OutlinedTextField(newField,{newField=it.map{c->if(c.isISOControl())' 'else c}.joinToString("").take(80)},Modifier.weight(1f).testTag("new-credential-field-name"),placeholder={Text("属性名称")},singleLine=true,shape=RoundedCornerShape(10.dp));OutlinedButton(onClick={val name=newField.trim();if(note.fields.any{it.name.equals(name,true)}||name in listOf("账号","账号 / 邮箱","密码","网站","备注"))vm.message("已存在同名属性")else{vm.edit(note.id){it.copy(fields=it.fields+CredentialField(name=name))};newField=""}},enabled=newField.isNotBlank()&&note.fields.size<128,modifier=Modifier.testTag("add-credential-field")){Text("添加新属性")}}
        Field("备注", note.content, lines = 4, readOnly = !editing) { text -> vm.edit(note.id) { it.copy(content = text) } }
    }
    removeField?.let{field->Confirm("删除「${field.name}」？","属性及内容将从这条记录中移除。",{removeField=null}){vm.edit(note.id){it.copy(fields=it.fields.filter{f->f.id!=field.id})};removeField=null}}
    if (generator) PasswordDialog(activity, { generator = false }) { password -> vm.edit(note.id) { it.copy(password = password) }; generator = false }
}

fun visibleText(context:android.content.Context,note:Note,values:Map<String,String> = note.variables.associate{it.id to it.name}):String = EditorContent.plain(PromptVariables.display(note))
