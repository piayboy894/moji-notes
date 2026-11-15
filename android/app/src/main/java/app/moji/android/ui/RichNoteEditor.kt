package app.moji.android.ui

import android.annotation.SuppressLint
import android.content.Intent
import android.graphics.Bitmap
import android.net.Uri
import android.webkit.*
import android.view.View
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.webkit.WebViewAssetLoader
import app.moji.android.*
import app.moji.android.data.*
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.*
import org.json.JSONObject
import kotlinx.coroutines.*

/** The Android host owns persistence and file permissions. Tiptap owns the entire document. */
class SharedEditorBridge(private val web:WebView,private val vm:MojiViewModel,private val id:String,private val epoch:Long,private val generation:Int) {
    private val pending=mutableMapOf<String,CompletableDeferred<Unit>>()
    val flusher:suspend ()->Unit={flush()}
    private suspend fun flush(){if(ready&&vm.workspaceEpoch==epoch&&vm.state.value.editorGeneration==generation){val token=uid();val done=CompletableDeferred<Unit>();pending[token]=done;try{web.evaluateJavascript("window.MojiEditor.flush(${JSONObject.quote(token)})",null);withTimeout(10000){done.await()}}finally{pending.remove(token)}}}
    var ready=false
    var loadSent=false
    var lastAppearance=""
    var fileResult:ValueCallback<Array<Uri>>?=null
    var onReady:(()->Unit)?=null
    @JavascriptInterface fun postMessage(message:String) {
        if(message.length>30_000_000)return
        web.post {
            runCatching {
                val data=json.parseToJsonElement(message).jsonObject
                val type=data["type"]?.jsonPrimitive?.content
                if(type=="flushed"){pending.remove(data["token"]?.jsonPrimitive?.content)?.complete(Unit);return@runCatching}
                if(vm.workspaceEpoch!=epoch||vm.state.value.editorGeneration!=generation)return@runCatching
                when(type){
                    "ready"->onReady?.invoke()
                    "loaded"->if(data["id"]?.jsonPrimitive?.content==id)ready=true
                    "change"->{if(data["id"]?.jsonPrimitive?.content!=id)return@runCatching
                        val content=data.getValue("content").jsonPrimitive.content
                        val variables=json.decodeFromJsonElement<List<PromptVariable>>(data.getValue("variables"))
                        if(!EditorContent.isDocument(content)||variables.any{!Regex("[a-f0-9]{32}").matches(it.id)||it.name.length !in 1..40})return@runCatching
                        val old=vm.state.value.workspace?.notes?.firstOrNull{it.id==id}?:return@runCatching
                        if(old.content!=content||old.variables!=variables)vm.edit(id){it.copy(content=content,variables=variables)}
                    }
                    "openLink"->{val uri=Uri.parse(data["url"]?.jsonPrimitive?.content?:"");if(uri.scheme in listOf("http","https"))runCatching{web.context.startActivity(Intent(Intent.ACTION_VIEW,uri))}}
                    "action"->if(vm.state.value.selectedId==id)vm.editorActions.tryEmit(data["action"]?.jsonPrimitive?.content?:"")
                }
            }
        }
    }
    fun command(action:String,value:String="null"){web.requestFocus();web.evaluateJavascript("window.MojiEditor?.action(${JSONObject.quote(action)},$value)",null)}
    fun load(note:Note,theme:String,fontSize:Double,p:Preferences){
        loadSent=true
        val payload=buildJsonObject{put("id",id);put("content",note.content);put("variables",json.encodeToJsonElement(note.variables));put("theme",theme);put("fontSize",fontSize);put("shortcuts",buildJsonObject{Shortcuts.actions.forEach{put(it.first,Shortcuts.binding(p,it.first))}});put("defaults",buildJsonObject{Shortcuts.actions.forEach{put(it.first,it.third)}})}
        web.evaluateJavascript("window.MojiEditor?window.MojiEditor.load($payload):window.__mojiLoad=$payload",null)
    }
}
@SuppressLint("SetJavaScriptEnabled")
@Composable fun RichNoteEditor(note:Note,mode:NoteMode,vm:MojiViewModel,modifier:Modifier=Modifier,usePrompt:()->Unit) {
    val prefs=vm.state.value.workspace!!.preferences
    val dark=MaterialTheme.colorScheme.background.red<.5f
    var bridge by remember(note.id){mutableStateOf<SharedEditorBridge?>(null)}
    var web by remember(note.id){mutableStateOf<WebView?>(null)}
    var variableDialog by remember(note.id){mutableStateOf(false)}
    var failure by remember(note.id){mutableStateOf(false)}
    val picker=rememberLauncherForActivityResult(ActivityResultContracts.StartActivityForResult()){result->
        bridge?.fileResult?.onReceiveValue(WebChromeClient.FileChooserParams.parseResult(result.resultCode,result.data));bridge?.fileResult=null
    }
    Column(modifier){
        if(mode==NoteMode.Prompt)Row(Modifier.fillMaxWidth().padding(horizontal=16.dp),horizontalArrangement=Arrangement.SpaceBetween){TextButton(onClick={variableDialog=true}){Text("插入变量")};Button(onClick=usePrompt){Text("使用提示词")}}
        if(failure)Text("编辑器未能加载，请重新打开记录",Modifier.padding(24.dp))
        AndroidView(modifier=Modifier.weight(1f).fillMaxWidth().testTag("shared-editor"),factory={context->
            WebView(context).apply {
                tag="MojiSharedEditor";id=View.generateViewId();isFocusable=true;isFocusableInTouchMode=true;setBackgroundColor(if(dark)android.graphics.Color.rgb(32,39,33)else android.graphics.Color.WHITE)
                setOnTouchListener { view,event->if(event.actionMasked==android.view.MotionEvent.ACTION_DOWN)view.requestFocus();false }
                settings.javaScriptEnabled=true;settings.domStorageEnabled=false;settings.allowFileAccess=false;settings.allowContentAccess=true;settings.mixedContentMode=WebSettings.MIXED_CONTENT_NEVER_ALLOW
                isVerticalScrollBarEnabled=false
                val loader=WebViewAssetLoader.Builder().addPathHandler("/assets/",WebViewAssetLoader.AssetsPathHandler(context)).build()
                val current=SharedEditorBridge(this,vm,note.id,vm.workspaceEpoch,vm.state.value.editorGeneration);bridge=current;web=this;vm.editorFlushers.add(current.flusher)
                current.onReady={if(!current.loadSent)current.load(vm.state.value.workspace?.notes?.firstOrNull{it.id==note.id}?:note,if(dark)"dark"else"light",prefs.fontSize,prefs)}
                addJavascriptInterface(current,"MojiNative")
                webViewClient=object:WebViewClient(){
                    override fun shouldInterceptRequest(view:WebView,request:WebResourceRequest):WebResourceResponse?=loader.shouldInterceptRequest(request.url)?:WebResourceResponse("text/plain","UTF-8",java.io.ByteArrayInputStream(ByteArray(0)))
                    override fun shouldOverrideUrlLoading(view:WebView,request:WebResourceRequest):Boolean = request.url.host!="appassets.androidplatform.net"
                    override fun onPageFinished(view:WebView,url:String){current.onReady?.invoke()}
                    override fun onReceivedError(view:WebView,request:WebResourceRequest,error:WebResourceError){if(request.isForMainFrame)failure=true}
                }
                webChromeClient=object:WebChromeClient(){
                    override fun onShowFileChooser(view:WebView,callback:ValueCallback<Array<Uri>>,params:FileChooserParams):Boolean {android.util.Log.d("MojiEditor","Opening system image picker");current.fileResult?.onReceiveValue(null);current.fileResult=callback;return try{picker.launch(params.createIntent().apply{action=Intent.ACTION_OPEN_DOCUMENT;addCategory(Intent.CATEGORY_OPENABLE);addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)});true}catch(e:Exception){current.fileResult=null;callback.onReceiveValue(null);vm.message("无法打开图片选择器");true}}
                    override fun onConsoleMessage(message:ConsoleMessage):Boolean {android.util.Log.d("MojiEditor",message.message());return true}
                }
                loadUrl("https://appassets.androidplatform.net/assets/editor/index.html")
            }
        },update={view->
            // The open editor owns its document. Never echo an autosave back into it.
            // Explicit version restores create a new editor session in EditorScreen.
            val current=bridge
            val appearance="${if(dark)"dark"else"light"}:${prefs.fontSize}"
            if(current!=null&&current.lastAppearance!=appearance){current.lastAppearance=appearance;view.evaluateJavascript("window.MojiEditor?.theme({theme:${JSONObject.quote(if(dark)"dark"else"light")},fontSize:${prefs.fontSize}})",null)}
        })
    }
    DisposableEffect(note.id){onDispose{val old=web;bridge?.fileResult?.onReceiveValue(null);val previous=bridge;if(previous!=null&&old!=null)vm.releaseEditor(previous.flusher){old.removeJavascriptInterface("MojiNative");old.destroy()}}}
    if(variableDialog){var name by remember{mutableStateOf("")};var error by remember{mutableStateOf("")};PanelDialog("插入变量",{variableDialog=false},actions={Button(onClick={try{val variable=PromptVariables.named(vm.selectedNote()?:note,name);bridge?.command("insertVariable",json.encodeToString(variable));variableDialog=false}catch(e:Exception){error=UserMessages.forError(e)}},enabled=name.isNotBlank()){Text("插入")}}){Field("变量名称",name,Modifier.testTag("variable-name")){name=it};if(error.isNotBlank())Text(error)}}
}
