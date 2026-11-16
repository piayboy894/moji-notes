package app.moji.android.ui

import android.annotation.SuppressLint
import android.content.Intent
import android.graphics.Bitmap
import android.net.Uri
import android.webkit.*
import android.view.View
import android.view.ActionMode
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.background
import androidx.compose.ui.graphics.Color
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

/** Chromium's selection handles are popup windows, outside Compose's z-order.
 * Treat a covering drawer/dialog as focus loss, without changing the document
 * or the editor's saved selection (used when inserting a prompt variable). */
class EditorWebView(context: android.content.Context) : WebView(context) {
    private var obscured = false
    private var selectionMode: ActionMode? = null
    override fun startActionMode(callback: ActionMode.Callback): ActionMode? =
        if (obscured) null else super.startActionMode(callback).also { selectionMode = it }
    override fun startActionMode(callback: ActionMode.Callback, type: Int): ActionMode? =
        if (obscured) null else super.startActionMode(callback, type).also { selectionMode = it }
    override fun onWindowFocusChanged(hasWindowFocus: Boolean) { super.onWindowFocusChanged(hasWindowFocus && !obscured) }
    fun cover(covered: Boolean) {
        if (obscured == covered) return
        obscured = covered
        if (covered) { selectionMode?.finish(); selectionMode = null; clearFocus() }
        super.onWindowFocusChanged(hasWindowFocus() && !covered)
    }
}

/** One page and browser per Activity; only the document changes. */
@SuppressLint("SetJavaScriptEnabled")
class SharedEditorSession(private val activity:MainActivity,private val vm:MojiViewModel) {
    private val main=android.os.Handler(android.os.Looper.getMainLooper())
    private val scope=CoroutineScope(SupervisorJob()+Dispatchers.Main.immediate)
    private val page=CompletableDeferred<Unit>()
    val isPreloaded get()=page.isCompleted
    // Attached behind the opaque Compose workspace so Chromium can paint its
    // first frame during startup, without stealing input or accessibility focus.
    private val warmHost=android.widget.FrameLayout(activity).apply {
        isFocusable=false;descendantFocusability=android.view.ViewGroup.FOCUS_BLOCK_DESCENDANTS
        importantForAccessibility=View.IMPORTANT_FOR_ACCESSIBILITY_NO_HIDE_DESCENDANTS
    }
    fun preload(){
        val root=activity.findViewById<android.view.ViewGroup>(android.R.id.content)
        if(warmHost.parent==null)root.addView(warmHost,0,android.view.ViewGroup.LayoutParams(-1,-1))
        park()
    }
    private fun park(){
        if(disposed||visible||web.parent==warmHost)return
        (web.parent as? android.view.ViewGroup)?.removeView(web);warmHost.addView(web)
        web.cover(true)
    }
    private fun reveal(expected:String,ready:()->Unit){
        web.postVisualStateCallback(version.toLong(),object:WebView.VisualStateCallback(){
            override fun onComplete(requestId:Long){if(!disposed&&token==expected){web.alpha=1f;ready()}}
        })
    }
    private val gate=kotlinx.coroutines.sync.Mutex()
    private val pending=mutableMapOf<String,CompletableDeferred<Unit>>()
    private var version=0;private var requested="";private var owner="";private var activeId="";private var token="";private var loadingToken="";private var epoch=-1L;private var generation=-1;private var visible=false;private var needsActivation=true;private var disposed=false
    private var loaded=CompletableDeferred<Unit>();private var lastAppearance=""
    var canConvert by mutableStateOf(false);private set
    var failure by mutableStateOf(false);private set
    var fileResult:ValueCallback<Array<Uri>>?=null
    var chooseFile:((Intent)->Unit)?=null
    private var fileToken=""
    private var selectedRange:JsonElement?=null
    val flusher:suspend ()->Unit={flush()}
    val web=EditorWebView(activity).apply {
        layoutParams=android.view.ViewGroup.LayoutParams(android.view.ViewGroup.LayoutParams.MATCH_PARENT,android.view.ViewGroup.LayoutParams.MATCH_PARENT)
        tag="MojiSharedEditor";id=View.generateViewId();isFocusable=true;isFocusableInTouchMode=true;alpha=0f
        if(BuildConfig.DEBUG)WebView.setWebContentsDebuggingEnabled(true)
        settings.javaScriptEnabled=true;settings.domStorageEnabled=false;settings.allowFileAccess=false;settings.allowContentAccess=true;settings.mixedContentMode=WebSettings.MIXED_CONTENT_NEVER_ALLOW;isVerticalScrollBarEnabled=false
        setOnTouchListener{v,e->if(e.actionMasked==android.view.MotionEvent.ACTION_DOWN)v.requestFocus();false}
        val loader=WebViewAssetLoader.Builder().addPathHandler("/assets/",WebViewAssetLoader.AssetsPathHandler(activity)).build()
        addJavascriptInterface(this@SharedEditorSession,"MojiNative")
        webViewClient=object:WebViewClient(){
            override fun shouldInterceptRequest(view:WebView,request:WebResourceRequest):WebResourceResponse?=loader.shouldInterceptRequest(request.url)?:WebResourceResponse("text/plain","UTF-8",java.io.ByteArrayInputStream(ByteArray(0)))
            override fun shouldOverrideUrlLoading(view:WebView,request:WebResourceRequest)=request.url.host!="appassets.androidplatform.net"
            override fun onReceivedError(view:WebView,request:WebResourceRequest,error:WebResourceError){if(request.isForMainFrame)failure=true}
        }
        webChromeClient=object:WebChromeClient(){
            override fun onShowFileChooser(view:WebView,callback:ValueCallback<Array<Uri>>,params:FileChooserParams):Boolean {
                fileResult?.onReceiveValue(null);fileResult=callback;fileToken=token
                return try{chooseFile?.invoke(params.createIntent().apply{action=Intent.ACTION_OPEN_DOCUMENT;addCategory(Intent.CATEGORY_OPENABLE);addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)})?:error("No active editor");true}catch(_:Exception){fileResult=null;callback.onReceiveValue(null);vm.message("无法打开图片选择器");true}
            }
            override fun onConsoleMessage(message:ConsoleMessage):Boolean {android.util.Log.d("MojiEditor",message.message());return true}
        }
        val initialDark=activity.resources.configuration.uiMode and android.content.res.Configuration.UI_MODE_NIGHT_MASK==android.content.res.Configuration.UI_MODE_NIGHT_YES
        val initialPalette=ThemeCatalog.resolve(vm.state.value.workspace?.preferences?:Preferences(),initialDark)
        setBackgroundColor(android.graphics.Color.parseColor(initialPalette.colors.getValue("Panel")))
        val hash=java.security.MessageDigest.getInstance("SHA-256").digest(activity.assets.open("editor/index.html").use{it.readBytes()}).joinToString(""){"%02x".format(it)}
        loadUrl("https://appassets.androidplatform.net/assets/editor/index.html?v=$hash&theme=${if(initialDark)"dark"else"light"}&bg=${initialPalette.colors.getValue("Panel").removePrefix("#")}")
    }
    init{vm.editorFlushers.add(flusher)}
    @JavascriptInterface fun postMessage(message:String){if(message.length>30_000_000)return;main.post{
        if(disposed)return@post
        runCatching{
            val data=json.parseToJsonElement(message).jsonObject;val type=data["type"]?.jsonPrimitive?.content
            if(type=="ready"){reveal(""){page.complete(Unit)};return@runCatching}
            if(type=="flushed"){pending.remove(data["token"]?.jsonPrimitive?.content)?.complete(Unit);return@runCatching}
            if(vm.workspaceEpoch!=epoch||vm.state.value.editorGeneration!=generation||data["id"]?.jsonPrimitive?.content!=activeId||data["session"]?.jsonPrimitive?.content!=token)return@runCatching
            when(type){
                "loaded"->if(token==loadingToken){val expected=token;reveal(expected){if(token==expected)loaded.complete(Unit)}}
                "selection"->{canConvert=visible&&loaded.isCompleted&&loadingToken==token&&data["canConvert"]?.jsonPrimitive?.boolean==true;selectedRange=if(canConvert)data["selection"]else null}
                "change"->{
                    val content=data.getValue("content").jsonPrimitive.content;val vars=json.decodeFromJsonElement<List<PromptVariable>>(data.getValue("variables"))
                    if(!EditorContent.isDocument(content)||vars.any{!Regex("[a-f0-9]{32}").matches(it.id)||it.name.length !in 1..2000})return@runCatching
                    val old=vm.state.value.workspace?.notes?.firstOrNull{it.id==activeId&&it.purgedAt==null}?:return@runCatching
                    if(old.content!=content||old.variables!=vars)vm.edit(activeId){it.copy(content=content,variables=vars)}
                }
                "action"->if(visible)vm.editorActions.tryEmit(data["action"]?.jsonPrimitive?.content?:"")
                "openLink"->if(visible){val uri=Uri.parse(data["url"]?.jsonPrimitive?.content?:"");if(uri.scheme in listOf("http","https"))runCatching{activity.startActivity(Intent(Intent.ACTION_VIEW,uri))}}
            }
        }
    }}
    fun attach(key:String):EditorWebView {owner=key;visible=true;(web.parent as? android.view.ViewGroup)?.removeView(web);return web}
    fun hide(key:String){if(owner!=key)return;visible=false;needsActivation=true;canConvert=false;chooseFile=null;web.cover(true);scope.launch{flush();if(owner!=key||visible)return@launch;web.evaluateJavascript("window.MojiEditor?.deactivate()",null);park()}}
    fun show(note:Note,dark:Boolean,obscured:Boolean,prefs:Preferences){
        visible=true;web.cover(obscured)
        val palette=ThemeCatalog.resolve(prefs,dark)
        web.setBackgroundColor(android.graphics.Color.parseColor(palette.colors.getValue("Panel")))
        val configuration=buildJsonObject{put("theme",if(dark)"dark"else"light");put("palette",palette.id);put("fontSize",prefs.fontSize);put("contentInset",23);put("shortcuts",buildJsonObject{Shortcuts.actions.forEach{put(it.first,Shortcuts.binding(prefs,it.first))}});put("defaults",buildJsonObject{Shortcuts.actions.forEach{put(it.first,it.third)}})}.toString()
        if(lastAppearance!=configuration){lastAppearance=configuration;web.evaluateJavascript("window.MojiEditor?.configure($configuration)",null)}
        val key="${vm.workspaceEpoch}:${vm.state.value.editorGeneration}:${note.id}"
        if(key==requested){if(loaded.isCompleted&&needsActivation){needsActivation=false;web.evaluateJavascript("window.MojiEditor?.activate()",null)};return}
        requested=key;if(activeId.isEmpty())web.alpha=0f;val request=++version;canConvert=false;loadingToken="";loaded.cancel();loaded=CompletableDeferred();needsActivation=false
        scope.launch {
            page.await();gate.lock();try{
                if(request!=version||disposed)return@launch
                flush();if(request!=version||disposed)return@launch
                fileResult?.onReceiveValue(null);fileResult=null
                val latest=vm.state.value.workspace?.notes?.firstOrNull{it.id==note.id}?:return@launch
                activeId=latest.id;epoch=vm.workspaceEpoch;generation=vm.state.value.editorGeneration;token=uid();loadingToken=token
                val payload=buildJsonObject{put("id",activeId);put("session",token);put("content",latest.content);put("variables",json.encodeToJsonElement(latest.variables));put("theme",if(dark)"dark"else"light");put("palette",palette.id);put("fontSize",prefs.fontSize);put("contentInset",23);put("shortcuts",buildJsonObject{Shortcuts.actions.forEach{put(it.first,Shortcuts.binding(prefs,it.first))}});put("defaults",buildJsonObject{Shortcuts.actions.forEach{put(it.first,it.third)}})}
                web.evaluateJavascript("window.MojiEditor.load($payload)",null)
            }finally{gate.unlock()}
        }
    }
    private suspend fun flush(){if(!page.isCompleted||activeId.isEmpty()||epoch!=vm.workspaceEpoch||generation!=vm.state.value.editorGeneration||disposed)return;val id=uid();val done=CompletableDeferred<Unit>();pending[id]=done;try{web.evaluateJavascript("window.MojiEditor.flush(${JSONObject.quote(id)})",null);withTimeout(10000){done.await()}}finally{pending.remove(id)}}
    fun clearSelectionAvailability(){selectedRange=null;canConvert=false}
    fun command(action:String){if(action=="convertVariable"&&!canConvert)return;val argument=if(action=="convertVariable")selectedRange.toString()else "null";val target=version;scope.launch{loaded.await();if(!visible||disposed||target!=version)return@launch;web.requestFocus();web.evaluateJavascript("window.MojiEditor.action(${JSONObject.quote(action)},$argument)",null);if(action=="focus"||action=="convertVariable")main.post{(activity.getSystemService(android.content.Context.INPUT_METHOD_SERVICE) as android.view.inputmethod.InputMethodManager).showSoftInput(web,android.view.inputmethod.InputMethodManager.SHOW_IMPLICIT)}}}
    fun imageResult(result:androidx.activity.result.ActivityResult){fileResult?.onReceiveValue(if(fileToken==token)WebChromeClient.FileChooserParams.parseResult(result.resultCode,result.data)else null);fileResult=null}
    fun dispose(){if(disposed)return;disposed=true;vm.editorFlushers.remove(flusher);fileResult?.onReceiveValue(null);scope.cancel();web.removeJavascriptInterface("MojiNative");(web.parent as? android.view.ViewGroup)?.removeView(web);(warmHost.parent as? android.view.ViewGroup)?.removeView(warmHost);web.destroy()}
}
@Composable fun RichNoteEditor(note:Note,mode:NoteMode,vm:MojiViewModel,modifier:Modifier=Modifier) {
    val activity=androidx.compose.ui.platform.LocalContext.current as MainActivity
    val session=activity.editorSession;val prefs=vm.state.collectAsState().value.workspace?.preferences?:Preferences();val dark=MaterialTheme.colorScheme.background.red<.5f;val obscured=LocalEditorObscured.current
    val owner=remember{uid()}
    val picker=rememberLauncherForActivityResult(ActivityResultContracts.StartActivityForResult(),session::imageResult)
    Column(modifier.background(MaterialTheme.colorScheme.surface)){
        if(mode==NoteMode.Prompt)Row(Modifier.fillMaxWidth().padding(horizontal=16.dp)){TextButton(onClick={session.command("convertVariable")},enabled=session.canConvert){Text("转化为变量")}}
        if(session.failure)Text("编辑器未能加载，请重新打开记录",Modifier.padding(24.dp))
        AndroidView(factory={session.attach(owner)},modifier=Modifier.weight(1f).fillMaxWidth().testTag("shared-editor"),update={session.chooseFile={picker.launch(it)};session.show(note,dark,obscured,prefs)},onReset=null,onRelease={session.hide(owner)})
    }
}
