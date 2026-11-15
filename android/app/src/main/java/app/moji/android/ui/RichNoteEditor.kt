package app.moji.android.ui

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Paint
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.painter.BitmapPainter
import androidx.compose.ui.input.key.*
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.*
import app.moji.android.MojiViewModel
import app.moji.android.data.*
import app.moji.android.security.Crypto
import com.mohamedrejeb.richeditor.annotation.ExperimentalRichTextApi
import com.mohamedrejeb.richeditor.model.*
import com.mohamedrejeb.richeditor.ui.BasicRichTextEditor
import kotlinx.coroutines.flow.drop
import kotlinx.coroutines.launch

private class RichCell(val original:String,plain:Boolean=false) {
    val state=RichTextState().apply{if(plain)setText(original)else setMarkdown(original)}
    val focus=FocusRequester()
    fun slice(start:Int,end:Int):String {val clone=RichTextState().apply{setHtml(state.toHtml())};val length=clone.annotatedString.length;if(end<length)clone.removeTextRange(TextRange(end,length));if(start>0)clone.removeTextRange(TextRange(0,start));return RichMarkdown.fromHtml(clone.toHtml())}
    private val initial=RichMarkdown.fromHtml(state.toHtml())
    fun markdown():String {val value=RichMarkdown.fromHtml(state.toHtml());return if(value==initial)original else value}
}
private class Piece(val block:RichBlock) {
    val key=uid(); val cell=RichCell(block.content,block.kind=="code")
    val rows=block.rows.map{row->row.map{RichCell(it)}}.toMutableStateList()
    var checked by mutableStateOf(block.checked)
    fun snapshot()=block.copy(content=if(block.kind=="code")cell.state.annotatedString.text else if(block.kind in listOf("image","rule"))block.content else cell.markdown(),rows=rows.map{row->row.map{it.markdown()}},checked=checked)
    fun cells()=if(block.kind=="table")rows.flatten() else if(block.kind in listOf("image","rule"))emptyList() else listOf(cell)
}
@OptIn(ExperimentalRichTextApi::class)
@Composable fun RichNoteEditor(note:Note,mode:NoteMode,vm:MojiViewModel,modifier:Modifier=Modifier,usePrompt:()->Unit) {
    val context=LocalContext.current;val scope=rememberCoroutineScope();val workspace=vm.state.collectAsState().value.workspace?:return
    val pieces=remember(note.id){RichBlocks.parse(note.content).map(::Piece).toMutableStateList()}
    var active by remember(note.id){mutableStateOf(pieces.firstOrNull()?.cell)}
    var lastSaved by remember(note.id){mutableStateOf(note.content)}
    var variableDialog by remember{mutableStateOf(false)};var linkDialog by remember{mutableStateOf(false)};var findDialog by remember{mutableStateOf(false)};var tableDialog by remember{mutableStateOf(false)};var removePiece by remember{mutableStateOf<Piece?>(null)}
    fun publish(){val value=RichBlocks.serialize(pieces.map{it.snapshot()});if(value!=lastSaved){lastSaved=value;vm.content(note.id,value)}}
    LaunchedEffect(note.content){if(note.content!=lastSaved){pieces.clear();pieces.addAll(RichBlocks.parse(note.content).map(::Piece));active=pieces.firstOrNull()?.cell;lastSaved=note.content}}
    fun insertBlock(block:RichBlock){
        val index=pieces.indexOfFirst{p->p.cells().any{it===active}}.let{if(it<0)pieces.lastIndex else it}
        val current=pieces.getOrNull(index);val inserted=Piece(block)
        if(current?.block?.kind=="text"){
            val cell=current.cell;val range=cell.state.selection;val before=cell.slice(0,range.min);val after=cell.slice(range.max,cell.state.annotatedString.length)
            pieces.removeAt(index);pieces.addAll(index,listOf(Piece(RichBlock(content=before)),inserted,Piece(RichBlock(content=after))))
        }else pieces.add(index+1,inserted)
        if(pieces.last().block.kind!="text")pieces.add(Piece(RichBlock()))
        active=inserted.cells().firstOrNull()?:pieces.last().cell;publish()
        scope.launch{kotlinx.coroutines.delay(80);runCatching{active?.focus?.requestFocus()}}
    }
    val imagePicker=rememberLauncherForActivityResult(ActivityResultContracts.GetContent()){uri->if(uri!=null)scope.launch{try{val bytes=context.contentResolver.openInputStream(uri)?.use{stream->val output=java.io.ByteArrayOutputStream();val buffer=ByteArray(8192);var count:Int;while(stream.read(buffer).also{count=it}>0){require(output.size()+count<=2_000_000){"单张图片请控制在 2 MB 以内"};output.write(buffer,0,count)};output.toByteArray()}?:error("无法读取图片");require(bytes.size<=2_000_000){"单张图片请控制在 2 MB 以内"};val mime=context.contentResolver.getType(uri)?:"image/png";require(mime in listOf("image/png","image/jpeg","image/gif","image/webp")){"图片格式不支持"};insertBlock(RichBlock("image","![图片](data:$mime;base64,${Crypto.b64(bytes)})"))}catch(e:Exception){vm.message(UserMessages.forError(e,"插入失败"))}}}
    fun heading(){val state=active?.state?:return;val value=state.annotatedString.text;val selection=state.selection;val start=value.lastIndexOf('\n',(selection.start-1).coerceAtLeast(0)).let{if(it<0)0 else it+1};val end=value.indexOf('\n',selection.end).let{if(it<0)value.length else it};state.selection=TextRange(start,end);state.toggleSpanStyle(SpanStyle(fontSize=1.5.em,fontWeight=FontWeight.Bold));state.selection=selection;publish()}
    fun action(id:String):Boolean {val state=active?.state;return when(id){"bold"->{state?.toggleSpanStyle(SpanStyle(fontWeight=FontWeight.Bold));publish();true};"undo"->{vm.undo(note.id);true};"redo"->{vm.redo(note.id);true};"find","replace"->{findDialog=true;true};else->false}}
    val loader=object:ImageLoader {
        @Composable override fun load(model:Any):ImageData? {
            val name=note.variables.firstOrNull{"moji-variable:"+it.id==model.toString()}?.name?:return null
            val density=LocalDensity.current;val size=with(density){14.sp.toPx()};val bitmap=remember(name,size){val paint=Paint(Paint.ANTI_ALIAS_FLAG).apply{textSize=size;color=android.graphics.Color.rgb(63,86,57)};val padding=with(density){8.dp.toPx()};val width=(paint.measureText(name)+padding*2).toInt().coerceAtLeast(20);val height=(size*1.8f).toInt();Bitmap.createBitmap(width,height,Bitmap.Config.ARGB_8888).also{val canvas=Canvas(it);val bg=Paint(Paint.ANTI_ALIAS_FLAG).apply{color=android.graphics.Color.rgb(230,237,222)};canvas.drawRoundRect(0f,0f,width.toFloat(),height.toFloat(),padding,padding,bg);canvas.drawText(name,padding,height/2f-(paint.ascent()+paint.descent())/2f,paint)}}
            return ImageData(BitmapPainter(bitmap.asImageBitmap()),contentDescription="变量：$name")
        }
    }
    CompositionLocalProvider(LocalImageLoader provides loader){
        Column(modifier.onPreviewKeyEvent{event->if(event.type!=KeyEventType.KeyDown||variableDialog||linkDialog||findDialog||tableDialog||removePiece!=null)false else {val chord=keyChord(event);val resolved=Shortcuts.resolve(workspace.preferences,chord);if(resolved!=null)action(resolved)else Shortcuts.actions.any{it.third==chord&&Shortcuts.binding(workspace.preferences,it.first)!=chord}}}) {
            HorizontalDivider(color=MaterialTheme.colorScheme.outlineVariant)
            Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(horizontal=8.dp),verticalAlignment=Alignment.CenterVertically){
                Tool(MojiIcons.Title,"标题",action=::heading);Tool(MojiIcons.FormatBold,"加粗",selected=active?.state?.currentSpanStyle?.fontWeight==FontWeight.Bold){action("bold")};Tool(MojiIcons.FormatItalic,"斜体",selected=active?.state?.currentSpanStyle?.fontStyle==FontStyle.Italic){active?.state?.toggleSpanStyle(SpanStyle(fontStyle=FontStyle.Italic));publish()};Tool(MojiIcons.FormatStrikethrough,"删除线",selected=active?.state?.currentSpanStyle?.textDecoration?.contains(TextDecoration.LineThrough)==true){active?.state?.toggleSpanStyle(SpanStyle(textDecoration=TextDecoration.LineThrough));publish()}
                Tool(MojiIcons.FormatListBulleted,"项目列表",selected=active?.state?.isUnorderedList==true){active?.state?.toggleUnorderedList();publish()};Tool(MojiIcons.FormatListNumbered,"编号列表",selected=active?.state?.isOrderedList==true){active?.state?.toggleOrderedList();publish()};Tool(MojiIcons.Checklist,"待办事项"){insertBlock(RichBlock("task"))};Tool(MojiIcons.FormatQuote,"引用"){insertBlock(RichBlock("quote"))}
                Tool(MojiIcons.Link,"插入链接"){linkDialog=true};Tool(MojiIcons.Image,"插入图片"){imagePicker.launch("image/*")};Tool(MojiIcons.TableChart,"插入表格"){tableDialog=true}
                Tool(MojiIcons.Undo,"撤销",enabled=vm.undo[note.id]?.isNotEmpty()==true){action("undo")};Tool(MojiIcons.Redo,"重做",enabled=vm.redo[note.id]?.isNotEmpty()==true){action("redo")};Tool(MojiIcons.FindReplace,"查找与替换"){findDialog=true}
            }
            if(active?.state?.isLink==true)Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState())){TextButton(onClick={val url=active?.state?.selectedLinkUrl?:return@TextButton;if(url.startsWith("https://")||url.startsWith("http://"))runCatching{context.startActivity(android.content.Intent(android.content.Intent.ACTION_VIEW,android.net.Uri.parse(url)))}}){Text("打开链接")};TextButton(onClick={linkDialog=true}){Text("修改链接")};TextButton(onClick={active?.state?.removeLink();publish()}){Text("移除链接")}}
            if(mode==NoteMode.Prompt)Row(Modifier.fillMaxWidth().padding(horizontal=16.dp),verticalAlignment=Alignment.CenterVertically){TextButton(onClick={variableDialog=true}){Text("插入变量")};Spacer(Modifier.weight(1f));Button(onClick=usePrompt){Text("使用提示词")}}
            HorizontalDivider(color=MaterialTheme.colorScheme.outlineVariant)
            Column(Modifier.weight(1f).fillMaxWidth().verticalScroll(rememberScrollState()).padding(22.dp),verticalArrangement=Arrangement.spacedBy(12.dp)){
                pieces.forEachIndexed{index,piece->key(piece.key){
                    @Composable fun Cell(cell:RichCell,mod:Modifier=Modifier,plain:Boolean=false){BasicRichTextEditor(state=cell.state,onValueChange={publish()},modifier=mod.fillMaxWidth().focusRequester(cell.focus).onFocusChanged{if(it.isFocused)active=cell}.onPreviewKeyEvent{event->if(piece.block.kind=="table"&&event.type==KeyEventType.KeyDown&&event.key==Key.Tab){val cells=piece.cells();val at=cells.indexOf(cell);cells[(at+(if(event.isShiftPressed)cells.size-1 else 1))%cells.size].focus.requestFocus();true}else false},textStyle=MaterialTheme.typography.bodyLarge.copy(color=MaterialTheme.colorScheme.onSurface,fontSize=workspace.preferences.fontSize.sp,lineHeight=(workspace.preferences.fontSize*1.8).sp,fontFamily=if(plain)FontFamily.Monospace else FontFamily.Default),minLines=1)}
                    when(piece.block.kind){
                        "table"->{
                            Column(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState())){piece.rows.forEachIndexed{ri,row->Row{row.forEachIndexed{ci,cell->Box(Modifier.width(160.dp).border(1.dp,MaterialTheme.colorScheme.outlineVariant).padding(10.dp)){Cell(cell,Modifier.testTag("table-cell-$ri-$ci"))}}}}}
                            Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState())){
                                TextButton(onClick={val row=piece.rows.indexOfFirst{it.any{c->c===active}}.let{if(it<0)piece.rows.lastIndex else it};piece.rows.add(row+1,List(piece.rows.firstOrNull()?.size?:2){RichCell("")});publish()}){Text("添加行")}
                                TextButton(onClick={val col=piece.rows.firstOrNull{it.any{c->c===active}}?.indexOf(active)?:((piece.rows.firstOrNull()?.size?:1)-1);for(i in piece.rows.indices){val cells=piece.rows[i].toMutableList();cells.add((col+1).coerceIn(0,cells.size),RichCell(""));piece.rows[i]=cells};publish()}){Text("添加列")}
                                TextButton(onClick={val row=piece.rows.indexOfFirst{it.any{c->c===active}};if(row>=0&&piece.rows.size>1){piece.rows.removeAt(row);active=piece.rows.first()[0];publish()}else removePiece=piece},enabled=piece.cells().any{it===active}){Text("删除行")}
                                TextButton(onClick={val col=piece.rows.firstOrNull{it.any{c->c===active}}?.indexOf(active)?:-1;if(col>=0&&(piece.rows.firstOrNull()?.size?:0)>1){for(i in piece.rows.indices)piece.rows[i]=piece.rows[i].filterIndexed{j,_->j!=col};active=piece.rows.first()[0];publish()}else removePiece=piece},enabled=piece.cells().any{it===active}){Text("删除列")}
                                TextButton(onClick={removePiece=piece}){Text("删除表格")}
                            }
                        }
                        "task"->Row(verticalAlignment=Alignment.CenterVertically){Checkbox(piece.checked,{piece.checked=it;publish()},Modifier.testTag("task-check"));Cell(piece.cell,Modifier.weight(1f).testTag("task-text"))}
                        "quote"->Row{Box(Modifier.width(3.dp).height(40.dp).background(MaterialTheme.colorScheme.primary));Cell(piece.cell,Modifier.padding(start=14.dp))}
                        "code"->Surface(color=MaterialTheme.colorScheme.surfaceVariant){Cell(piece.cell,Modifier.padding(14.dp),true)}
                        "image"->{val uri=piece.block.content.substringAfter("](").dropLast(1);val bitmap=remember(uri){if(uri.startsWith("data:image/"))runCatching{val bytes=Crypto.unb64(uri.substringAfter(','));BitmapFactory.decodeByteArray(bytes,0,bytes.size)?.asImageBitmap()}.getOrNull()else null};if(bitmap!=null)Image(bitmap,"图片",Modifier.fillMaxWidth().heightIn(max=400.dp))else TextButton(onClick={if(uri.startsWith("https://")||uri.startsWith("http://"))runCatching{context.startActivity(android.content.Intent(android.content.Intent.ACTION_VIEW,android.net.Uri.parse(uri)))}}){Text("打开图片链接")};TextButton(onClick={removePiece=piece}){Text("删除图片")}}
                        "rule"->HorizontalDivider()
                        else->Cell(piece.cell,Modifier.heightIn(min=if(index==pieces.lastIndex)180.dp else 32.dp).testTag(if(index==0)"note-content" else "note-content-${piece.key}"))
                    }
                }}
            }
        }
    }
    if(removePiece!=null)Confirm("删除这项内容？","可以通过撤销恢复。",{removePiece=null}){pieces.remove(removePiece);if(pieces.isEmpty()||pieces.last().block.kind!="text")pieces.add(Piece(RichBlock()));active=pieces.last().cell;publish()}
    if(tableDialog){var rows by remember{mutableIntStateOf(3)};var columns by remember{mutableIntStateOf(2)};PanelDialog("插入表格",{tableDialog=false},actions={Button(onClick={insertBlock(RichBlock("table",rows=List(rows){List(columns){""}}));tableDialog=false}){Text("插入")}}){Row(verticalAlignment=Alignment.CenterVertically){Text("行数",Modifier.weight(1f));TextButton(onClick={rows=(rows-1).coerceAtLeast(1)}){Text("−")};Text("$rows");TextButton(onClick={rows=(rows+1).coerceAtMost(20)}){Text("+")}};Row(verticalAlignment=Alignment.CenterVertically){Text("列数",Modifier.weight(1f));TextButton(onClick={columns=(columns-1).coerceAtLeast(1)}){Text("−")};Text("$columns");TextButton(onClick={columns=(columns+1).coerceAtMost(10)}){Text("+")}}}}
    if(variableDialog){var name by remember{mutableStateOf("")};var error by remember{mutableStateOf("")};PanelDialog("插入变量",{variableDialog=false},actions={Button(onClick={try{val current=vm.selectedNote()?:note;val variable=PromptVariables.named(current,name);if(current.variables.none{it.id==variable.id})vm.edit(note.id){it.copy(variables=it.variables+variable)};active?.state?.insertHtmlAfterSelection("<img src=\"moji-variable:${variable.id}\" width=\"0\" height=\"0\">");publish();variableDialog=false}catch(e:Exception){error=UserMessages.forError(e,"插入失败")}},enabled=name.isNotBlank()){Text("插入")}}){Field("变量名称",name,Modifier.testTag("variable-name")){name=it};if(error.isNotBlank())Text(error,color=MaterialTheme.colorScheme.error)}}
    if(linkDialog){var label by remember{mutableStateOf(active?.state?.let{it.annotatedString.text.substring(it.selection.min,it.selection.max)}?:"")};var url by remember{mutableStateOf(active?.state?.selectedLinkUrl?:"https://")};PanelDialog("插入链接",{linkDialog=false},actions={Button(onClick={active?.state?.let{state->if(state.isLink)state.updateLink(url)else{state.removeSelectedText();state.addLink(label.ifBlank{url},url)}};publish();linkDialog=false},enabled=url.startsWith("https://")||url.startsWith("http://")){Text("插入")}}){Field("显示文字",label){label=it};Field("链接地址",url){url=it}}}
    if(findDialog){var query by remember{mutableStateOf("")};var replace by remember{mutableStateOf("")};var message by remember{mutableStateOf("")};var index by remember{mutableIntStateOf(0)};PanelDialog("查找与替换",{findDialog=false},actions={TextButton(onClick={val cells=pieces.flatMap{it.cells()};val matches=cells.flatMap{cell->Regex(Regex.escape(query),RegexOption.IGNORE_CASE).findAll(cell.state.annotatedString.text).map{cell to it.range}.toList()};if(matches.isEmpty())message="没有找到匹配内容" else {val(cell,range)=matches[index++%matches.size];active=cell;cell.state.selection=TextRange(range.first,range.last+1);message="已选中匹配内容"}},enabled=query.isNotEmpty()){Text("下一个")};Button(onClick={var count=0;pieces.flatMap{it.cells()}.forEach{cell->val matches=Regex(Regex.escape(query),RegexOption.IGNORE_CASE).findAll(cell.state.annotatedString.text).toList();matches.asReversed().forEach{cell.state.replaceTextRange(TextRange(it.range.first,it.range.last+1),replace);count++}};publish();message="已替换 $count 处"},enabled=query.isNotEmpty()){Text("全部替换")}}){Field("查找",query){query=it;index=0};Field("替换为",replace){replace=it};if(message.isNotBlank())Text(message)}}
}
