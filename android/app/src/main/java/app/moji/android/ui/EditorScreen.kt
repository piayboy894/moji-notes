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
import io.noties.markwon.*
import io.noties.markwon.ext.tables.TablePlugin
import io.noties.markwon.ext.tasklist.TaskListPlugin
import io.noties.markwon.ext.strikethrough.StrikethroughPlugin
import kotlinx.coroutines.delay

@Composable fun EditorScreen(vm: MojiViewModel, s: AppState, note: Note, activity: MainActivity, export: (Note) -> Unit, modifier: Modifier = Modifier, showBack: Boolean = false, back: () -> Unit = {}) {
    val workspace=s.workspace?:return;val book=workspace.notebooks.first{it.id==note.notebookId};val editing=s.editingCredentialId==note.id
    var more by remember(note.id){mutableStateOf(false)};var history by remember(note.id){mutableStateOf(false)};var prompt by remember(note.id){mutableStateOf(false)};var move by remember(note.id){mutableStateOf(false)};var purge by remember(note.id){mutableStateOf(false)}
    Column(modifier.fillMaxSize().background(MaterialTheme.colorScheme.surface)){
        Row(Modifier.fillMaxWidth().padding(horizontal=12.dp,vertical=4.dp),verticalAlignment=Alignment.CenterVertically){
            if(showBack)Tool(MojiIcons.ArrowBack,"返回记录",action=back)
            Text(book.name,Modifier.weight(1f),maxLines=1,overflow=TextOverflow.Ellipsis,style=MaterialTheme.typography.labelSmall,color=color(book.color))
            if(note.deletedAt==null){
                if(book.mode==NoteMode.Password)Button(onClick={vm.editCredential(!editing)},modifier=Modifier.testTag("edit-credential"),contentPadding=PaddingValues(14.dp,8.dp)){Text(if(editing)"完成" else "编辑密码")}
                Tool(if(note.favorite)MojiIcons.Star else MojiIcons.StarOutline,"收藏记录"){vm.edit(note.id){it.copy(favorite=!it.favorite)}}
                Tool(MojiIcons.History,"版本历史"){history=true}
            }
            Box{Tool(MojiIcons.MoreHoriz,"更多操作"){more=true};DropdownMenu(more,{more=false}){
                if(note.deletedAt==null){
                    DropdownMenuItem(text={Text(if(note.pinned)"取消置顶" else "置顶记录")},onClick={vm.edit(note.id){it.copy(pinned=!it.pinned)};more=false})
                    DropdownMenuItem(text={Text("创建副本")},onClick={vm.duplicate(note);more=false})
                    DropdownMenuItem(text={Text(if(note.archived)"取消归档" else "归档记录")},onClick={vm.edit(note.id){it.copy(archived=!it.archived)};vm.clearSelection();more=false})
                    DropdownMenuItem(text={Text("移动到记事本")},onClick={move=true;more=false})
                    if(book.mode!=NoteMode.Password){DropdownMenuItem(text={Text("复制全文")},onClick={activity.copy(visibleText(activity,note));more=false});DropdownMenuItem(text={Text("导出")},onClick={export(note);more=false})}
                    DropdownMenuItem(text={Text("移到回收站")},onClick={vm.trash(setOf(note.id));more=false})
                }else DropdownMenuItem(text={Text("永久删除")},onClick={purge=true;more=false})
            }}
        }
        BasicTextField(note.title,{text->vm.edit(note.id){it.copy(title=text)}},Modifier.fillMaxWidth().padding(23.dp,18.dp).testTag("note-title"),textStyle=MaterialTheme.typography.titleLarge.copy(color=MaterialTheme.colorScheme.onSurface),cursorBrush=SolidColor(MaterialTheme.colorScheme.primary),readOnly=note.deletedAt!=null||(book.mode==NoteMode.Password&&!editing))
        if(note.deletedAt!=null)Column(Modifier.padding(24.dp),verticalArrangement=Arrangement.spacedBy(16.dp)){Button(onClick={vm.restore(setOf(note.id))}){Text("恢复记录")};TextButton(onClick={purge=true}){Text("永久删除")}}
        else if(book.mode==NoteMode.Password)CredentialEditor(vm,note,activity,editing,Modifier.weight(1f))
        else key(note.id) { RichNoteEditor(note,book.mode,vm,Modifier.weight(1f)){prompt=true} }
    }
    if(history)HistoryDialog(note,{history=false},allowRestore=book.mode!=NoteMode.Password||editing){vm.restoreVersion(note.id,it);history=false}
    if(prompt)PromptDialog(note,activity){prompt=false}
    if(move)PanelDialog("移动到记事本",{move=false}){val books=workspace.liveBooks.filter{it.mode==book.mode&&it.id!=book.id};if(books.isEmpty())Text("暂无同类型记事本");books.forEach{target->TextButton(onClick={vm.edit(note.id){it.copy(notebookId=target.id)};vm.navigate(target.id);vm.select(note.id);move=false}){Text(target.name)}}}
    if(purge)Confirm("永久删除这条记录？","正文、密码和历史版本会被清除，无法恢复。",{purge=false}){vm.purge(setOf(note.id))}
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
@Composable private fun CredentialEditor(vm: MojiViewModel, note: Note, activity: MainActivity, editing: Boolean, modifier: Modifier = Modifier) {
    var generator by remember { mutableStateOf(false) }
    Column(modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(23.dp, 20.dp), verticalArrangement = Arrangement.spacedBy(19.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) { Field("账号 / 邮箱", note.username, Modifier.weight(1f).testTag("account"), readOnly = !editing) { text -> vm.edit(note.id) { it.copy(username = text) } }; Tool(MojiIcons.ContentCopy, "复制账号") { activity.copy(note.username) } }
        Row(verticalAlignment = Alignment.CenterVertically) { Field("密码", note.password, Modifier.weight(1f).testTag("credential-password"), sensitive = true, readOnly = !editing) { text -> vm.edit(note.id) { it.copy(password = text) } }; Tool(MojiIcons.ContentCopy, "复制密码") { activity.copy(note.password, true) }; if (editing) Tool(MojiIcons.Casino, "生成强密码") { generator = true } }
        Row(verticalAlignment = Alignment.CenterVertically) { Field("网站", note.url, Modifier.weight(1f), readOnly = !editing) { text -> vm.edit(note.id) { it.copy(url = text) } }; Tool(MojiIcons.OpenInNew, "打开网站") { activity.openUrl(note.url) } }
        Field("备注", note.content, lines = 4, readOnly = !editing) { text -> vm.edit(note.id) { it.copy(content = text) } }
    }
    if (generator) PasswordDialog(activity, { generator = false }) { password -> vm.edit(note.id) { it.copy(password = password) }; generator = false }
}

fun visibleText(context:android.content.Context,note:Note,values:Map<String,String> = note.variables.associate{it.id to it.name}):String = PromptVariables.plainText(note,values){Markwon.builder(context).usePlugin(TablePlugin.create(context)).usePlugin(TaskListPlugin.create(context)).usePlugin(StrikethroughPlugin.create()).build().toMarkdown(it).toString()}
