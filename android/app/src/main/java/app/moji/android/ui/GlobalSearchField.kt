package app.moji.android.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.key.*
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.PopupProperties
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import app.moji.android.*
import app.moji.android.data.*
import kotlinx.coroutines.*

private data class SearchHit(val id:String,val bookId:String,val title:String,val detail:String,val mode:NoteMode,val archived:Boolean)
private data class SearchMatches(val hits:List<SearchHit> = emptyList(),val ready:Boolean=false)
@Composable fun GlobalSearchField(s:AppState,vm:MojiViewModel,request:Int,active:Boolean,chosen:()->Unit,visible:(Boolean)->Unit){
    val w=s.workspace?:return
    var input by remember{mutableStateOf(TextFieldValue(""))};var expanded by remember{mutableStateOf(false)};var selected by remember{mutableIntStateOf(0)};var openWhenReady by remember{mutableStateOf(false)};var consumed by remember{mutableIntStateOf(0)}
    val focus=remember{FocusRequester()};val focusManager=LocalFocusManager.current;val keyboard=LocalSoftwareKeyboardController.current
    val menuScroll=rememberScrollState();val density=LocalDensity.current
    val visibility by rememberUpdatedState(visible)
    val matches by produceState(SearchMatches(),w.notes,w.notebooks,input.text){
        value=SearchMatches();delay(100);val query=input.text.trim()
        val hits=withContext(Dispatchers.Default){val books=w.liveBooks.associateBy{it.id};w.notes.asSequence().filter{ensureActive();it.deletedAt==null&&it.purgedAt==null&&it.notebookId in books}.filter{note->val book=books.getValue(note.notebookId);book.name.contains(query,true)||Records.matches(note,query,book.mode)}.sortedByDescending{it.updatedAt}.take(80).map{note->val book=books.getValue(note.notebookId);SearchHit(note.id,book.id,note.title.ifBlank{"未命名记录"},book.name+" · "+Records.relative(note.updatedAt)+(if(note.archived)" · 归档"else""),book.mode,note.archived)}.toList()}
        value=SearchMatches(hits,true)
    }
    val results=matches.hits
    fun dismiss(){openWhenReady=false;expanded=false;focusManager.clearFocus();keyboard?.hide()}
    fun open(hit:SearchHit?){if(hit==null)return;dismiss();vm.navigate(if(hit.archived)"archive"else hit.bookId);vm.select(hit.id);chosen()}
    fun submit(){val hit=results.getOrNull(selected)?:results.firstOrNull();if(hit!=null)open(hit)else if(!matches.ready)openWhenReady=true}
    LaunchedEffect(matches){if(matches.ready&&openWhenReady){openWhenReady=false;open(matches.hits.firstOrNull())}}
    LaunchedEffect(request,active){if(active&&request>consumed){consumed=request;input=input.copy(selection=TextRange(0,input.text.length));focus.requestFocus();expanded=true;keyboard?.show()}}
    LaunchedEffect(input.text){selected=0}
    LaunchedEffect(active){if(!active)expanded=false}
    SideEffect{visibility(expanded&&active)}
    DisposableEffect(Unit){onDispose{visibility(false)}}
    LaunchedEffect(selected,expanded,results,menuScroll.viewportSize){if(expanded&&menuScroll.viewportSize>0){val row=with(density){64.dp.toPx()};val padding=with(density){8.dp.toPx()};val top=padding+selected*row;val bottom=top+row;val target=if(top<menuScroll.value)top else if(bottom>menuScroll.value+menuScroll.viewportSize)bottom-menuScroll.viewportSize else menuScroll.value.toFloat();menuScroll.animateScrollTo(target.toInt().coerceAtLeast(0))}}
    BackHandler(enabled=expanded&&active){dismiss()}
    Box(Modifier.fillMaxWidth()){
        OutlinedTextField(input,{input=it;expanded=true;openWhenReady=false},modifier=Modifier.fillMaxWidth().testTag("global-search").focusRequester(focus).onFocusChanged{if(it.isFocused)expanded=true;else expanded=false}.onPreviewKeyEvent{event->
            if(event.type!=KeyEventType.KeyDown)false else when(event.key){
                Key.DirectionDown->{if(!expanded){expanded=true;selected=0}else selected=(selected+1).coerceAtMost((results.size-1).coerceAtLeast(0));true}
                Key.DirectionUp->{selected=(selected-1).coerceAtLeast(0);true}
                Key.Enter->{submit();true}
                Key.Escape->{dismiss();true}
                else->false
            }
        },singleLine=true,textStyle=MaterialTheme.typography.bodyMedium,shape=RoundedCornerShape(10.dp),placeholder={Text("搜索",style=MaterialTheme.typography.bodyMedium)},leadingIcon={Icon(MojiIcons.Search,null,Modifier.size(20.dp))},trailingIcon=if(input.text.isNotEmpty())({IconButton(onClick={input=TextFieldValue("");focus.requestFocus();expanded=true}){Icon(MojiIcons.Close,"清除搜索",Modifier.size(18.dp))}})else null,keyboardOptions=KeyboardOptions(imeAction=ImeAction.Search),keyboardActions=KeyboardActions(onSearch={submit()}))
        DropdownMenu(expanded=expanded&&active,onDismissRequest={expanded=false},properties=PopupProperties(focusable=false),scrollState=menuScroll,modifier=Modifier.widthIn(min=220.dp,max=340.dp).heightIn(max=340.dp).testTag("global-search-results")){
            if(!matches.ready)Box(Modifier.width(250.dp).height(48.dp),contentAlignment=androidx.compose.ui.Alignment.Center){CircularProgressIndicator(Modifier.size(16.dp),strokeWidth=2.dp)}
            results.forEachIndexed{index,hit->val marked=index==selected
                DropdownMenuItem(onClick={open(hit)},modifier=Modifier.height(64.dp).background(if(marked)color(LocalMojiPalette.current.colors.getValue("RecordSelected"))else Color.Transparent,RoundedCornerShape(7.dp)).testTag("global-result-${hit.id}"),text={Column(verticalArrangement=Arrangement.spacedBy(5.dp)){Text(hit.title,maxLines=1,overflow=TextOverflow.Ellipsis,color=if(marked)color(LocalMojiPalette.current.colors.getValue("RecordSelectedInk"))else MaterialTheme.colorScheme.onSurface);Text(hit.detail,style=MaterialTheme.typography.labelSmall,maxLines=1,overflow=TextOverflow.Ellipsis,color=if(marked)color(LocalMojiPalette.current.colors.getValue("RecordSelectedSub"))else MaterialTheme.colorScheme.onSurfaceVariant)}},leadingIcon={Icon(modeIcon(hit.mode),null,Modifier.size(19.dp),tint=if(marked)color(LocalMojiPalette.current.colors.getValue("RecordSelectedInk"))else MaterialTheme.colorScheme.onSurfaceVariant)})
            }
            if(results.isEmpty()&&matches.ready)DropdownMenuItem(text={Text(if(input.text.isBlank())"暂无记录"else"没有找到记录")},onClick={},enabled=false)
        }
    }
}
