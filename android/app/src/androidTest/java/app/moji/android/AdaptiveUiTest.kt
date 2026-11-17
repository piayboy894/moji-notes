package app.moji.android

import androidx.compose.ui.graphics.toPixelMap
import androidx.compose.ui.graphics.toArgb
import android.content.pm.ActivityInfo
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.input.key.Key
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import app.moji.android.data.*
import app.moji.android.sync.GameSaves
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.launch
import androidx.lifecycle.lifecycleScope
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@OptIn(androidx.compose.ui.test.ExperimentalTestApi::class)
@RunWith(AndroidJUnit4::class)
class AdaptiveUiTest {
    @get:Rule val compose = createAndroidComposeRule<MainActivity>()
    @Test fun launcherUsesAdaptiveWhiteTileAndContainsNoBlackExterior(){
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val drawable=context.packageManager.getApplicationIcon(context.applicationInfo)
        assertTrue("Launcher must resolve the adaptive icon",drawable is android.graphics.drawable.AdaptiveIconDrawable)
        if(android.os.Build.VERSION.SDK_INT>=33)assertNotNull((drawable as android.graphics.drawable.AdaptiveIconDrawable).monochrome)
        val bitmap=android.graphics.Bitmap.createBitmap(256,256,android.graphics.Bitmap.Config.ARGB_8888)
        drawable.setBounds(0,0,256,256);drawable.draw(android.graphics.Canvas(bitmap))
        var blue=0;var black=0
        for(y in 0 until 256)for(x in 0 until 256){val c=bitmap.getPixel(x,y);if(android.graphics.Color.alpha(c)>200){val r=android.graphics.Color.red(c);val g=android.graphics.Color.green(c);val b=android.graphics.Color.blue(c);if(b>r+35)blue++;if(r<35&&g<35&&b<35)black++}}
        assertTrue("Blue-purple brush mark must remain visible",blue>1000)
        assertEquals("Black surroundings must not be baked into the launcher",0,black)
    }
    @Test fun notebookOverviewOpensARecordAndReturnsWithoutLosingSearchOrEditor(){
        try{
            resize(1440,1080,160);ready(false);val book=Notebook(name="Overview");val note=Note(notebookId=book.id,title="Find this record",content="Overview body")
            runBlocking{compose.activity.vm.replaceData(GameSaves.capture(Workspace(notebooks=listOf(book),notes=listOf(note))))};compose.activityRule.scenario.onActivity{it.vm.navigate(book.id)}
            compose.onNodeWithTag("notebook-overview").assertExists();assertNull(compose.activity.vm.state.value.selectedId)
            compose.onNodeWithTag("list-search").performTextInput("Find this");hideKeyboard();compose.onNodeWithTag("record-card-${note.id}").performClick();webReady();val native=webView()
            compose.onNodeWithTag("back-to-overview").performClick();compose.onNodeWithTag("notebook-overview").assertExists();assertEquals("Find this",compose.activity.vm.state.value.search);assertNull(compose.activity.vm.state.value.selectedId)
            screenshot("41-notebook-overview");compose.onNodeWithTag("record-card-${note.id}").performClick();webReady();assertSame(native,webView());assertEquals("Overview body",jsText())
        }finally{shell("wm size reset");shell("wm density reset")}
    }
    @Test fun lateOverviewCardIsVisibleInTheDetailListAndItsEditorDoesNotMoveOnRecordChanges(){
        val previousDuration=shell("settings get global animator_duration_scale").trim()
        try{
            // CI disables system animations globally. This test deliberately
            // samples the real transition, then restores the previous setting.
            shell("settings put global animator_duration_scale 1")
            resize(1440,1080,160);ready(false);val book=Notebook(name="Continuity");val notes=(0 until 120).map{Note(notebookId=book.id,title="Record %03d".format(it),content="Body $it")}
            runBlocking{compose.activity.vm.replaceData(GameSaves.capture(Workspace(notebooks=listOf(book),notes=notes)))}
            compose.activityRule.scenario.onActivity{it.vm.navigate(book.id);it.vm.sort("name")};val browser=webView()
            // Pick the third column of a late row, not an already aligned card.
            compose.onNodeWithTag("note-list").performScrollToIndex(100);val target=notes[102]
            compose.mainClock.autoAdvance=false
            compose.onNodeWithTag("record-card-${target.id}").performClick()
            compose.mainClock.advanceTimeBy(80);compose.waitForIdle()
            screenshot("44-editor-pane-entry-early")
            val start=InstrumentationRegistry.getInstrumentation().uiAutomation.takeScreenshot()!!
            compose.mainClock.advanceTimeBy(100);compose.waitForIdle()
            screenshot("45-editor-pane-entry-middle")
            val middle=InstrumentationRegistry.getInstrumentation().uiAutomation.takeScreenshot()!!
            compose.mainClock.advanceTimeBy(220);compose.waitForIdle()
            // Compose's clock advances the draw state; SurfaceFlinger presents
            // it asynchronously. Sample the same presented frame we archive.
            screenshot("46-editor-pane-entry-settled")
            val finish=InstrumentationRegistry.getInstrumentation().uiAutomation.takeScreenshot()!!
            // Overlay drawing moves independently of semantics layout bounds.
            // Inspect its actual opaque background edge on the device instead.
            // The settled WebView paints a scrollbar at the outer edge. Scan
            // from the same blank inset used to sample the panel background.
            val y=finish.height-100;val sampleX=finish.width-16;val panel=finish.getPixel(sampleX,y)
            fun edge(bitmap:android.graphics.Bitmap):Int {var x=sampleX;while(x>=0){val c=bitmap.getPixel(x,y);if(kotlin.math.abs(android.graphics.Color.red(c)-android.graphics.Color.red(panel))>3||kotlin.math.abs(android.graphics.Color.green(c)-android.graphics.Color.green(panel))>3||kotlin.math.abs(android.graphics.Color.blue(c)-android.graphics.Color.blue(panel))>3)break;x--};return x+1}
            val startEdge=edge(start);val middleEdge=edge(middle);val finalEdge=edge(finish)
            val settled=compose.onNodeWithTag("record-card-${target.id}").fetchSemanticsNode().boundsInRoot
            val selectedColor=finish.getPixel(settled.center.x.toInt(),(settled.bottom-8).toInt())
            fun cardLeft(bitmap:android.graphics.Bitmap):Int {
                var left=bitmap.width
                for(y in settled.top.toInt() until (settled.bottom+120).toInt().coerceAtMost(bitmap.height))
                    for(x in (settled.left-4).toInt().coerceAtLeast(0) until left)
                        if(bitmap.getPixel(x,y)==selectedColor){left=x;break}
                return left
            }
            val startCard=cardLeft(start);val finalCard=cardLeft(finish);val frameWidth=finish.width
            start.recycle();middle.recycle();finish.recycle()
            assertTrue("Whole opaque pane must move: $startEdge > $middleEdge > $finalEdge",startEdge>middleEdge&&middleEdge>finalEdge)
            assertTrue("Right-column card must travel to the list: $startCard > $finalCard",startCard>finalCard+12&&startCard<frameWidth)
            compose.mainClock.autoAdvance=true;webReady()
            fun assertVisible(id:String){val card=compose.onNodeWithTag("record-card-$id").assertIsDisplayed().fetchSemanticsNode().boundsInRoot;val list=compose.onNodeWithTag("note-list").fetchSemanticsNode().boundsInRoot;assertTrue("Opened record must be fully visible",card.top>=list.top-.5f&&card.bottom<=list.bottom+.5f)}
            assertVisible(target.id);assertSame(browser,webView());screenshot("43-late-record-visible")
            compose.onNodeWithTag("back-to-overview").performClick();compose.onNodeWithTag("record-card-${target.id}").assertIsDisplayed().performClick();webReady();assertVisible(target.id)
            val before=compose.onNodeWithTag("shared-editor").fetchSemanticsNode().boundsInRoot;val next=notes[103];compose.onNodeWithTag("record-card-${next.id}").performClick();webReady()
            assertSame(browser,webView());assertEquals(before,compose.onNodeWithTag("shared-editor").fetchSemanticsNode().boundsInRoot);assertEquals("Body 103",jsText())
        }finally{compose.mainClock.autoAdvance=true;shell("settings put global animator_duration_scale $previousDuration");shell("wm size reset");shell("wm density reset")}
    }
    private fun shell(command: String): String = InstrumentationRegistry.getInstrumentation().uiAutomation.executeShellCommand(command).use { d -> android.os.ParcelFileDescriptor.AutoCloseInputStream(d).use { it.readBytes().toString(Charsets.UTF_8) } }
    private fun resize(w: Int,h: Int,d: Int) { shell("wm size ${w}x$h"); shell("wm density $d"); Thread.sleep(1200); compose.waitForIdle() }
    private fun ready(books: Boolean = true) {
        compose.waitUntil(15000) { val s=compose.activity.vm.state.value; check(s.error.isBlank()){s.error}; s.workspace!=null && !s.loading }
        // Test fixtures are created explicitly; production starts with no notebooks.
        if (books) compose.activityRule.scenario.onActivity { a ->
            NoteMode.entries.forEach { mode -> if (a.vm.state.value.workspace!!.liveBooks.none { it.mode==mode }) a.vm.saveBook(null,"测试${mode.label}",mode,"#75876D") }
        }
    }
    private fun webView():android.webkit.WebView { var result:android.webkit.WebView?=null;compose.activityRule.scenario.onActivity{a->fun find(v:android.view.View){if(v is android.webkit.WebView&&v.tag=="MojiSharedEditor")result=v;else if(v is android.view.ViewGroup)for(i in 0 until v.childCount)find(v.getChildAt(i))};find(a.window.decorView)};return result?:error("Shared editor is not attached") }
    private fun js(code:String):String {val done=java.util.concurrent.CountDownLatch(1);var result="";val web=webView();compose.activityRule.scenario.onActivity{web.evaluateJavascript(code){result=it;done.countDown()}};check(done.await(10,java.util.concurrent.TimeUnit.SECONDS)){"Editor script timed out"};return result}
    private fun focusWeb(){val web=webView();compose.activityRule.scenario.onActivity{web.requestFocus()};js("window.MojiEditor.editor.commands.focus()");compose.waitUntil(5000){js("Boolean(document.activeElement?.matches('.tiptap'))")=="true"};compose.waitForIdle();Thread.sleep(350)}
    private fun jsText():String=org.json.JSONTokener(js("window.MojiEditor.editor.getText() ")).nextValue().toString()
    private fun webReady(){compose.waitForIdle();val expected=org.json.JSONObject.quote(compose.activity.vm.state.value.selectedId);compose.waitUntil(30000){runCatching{js("Boolean(window.MojiEditor?.editor && window.MojiEditor.editor.isEditable && window.innerHeight>0 && document.body.clientHeight>=window.innerHeight*.8)")=="true"&&js("window.MojiEditor.recordId")==expected}.getOrDefault(false)}}
    private fun tapWeb(selector:String,longPress:Boolean=false){hideKeyboard();val native=webView();compose.activityRule.scenario.onActivity{native.scrollTo(0,0)};js("document.querySelector('.simple-editor-wrapper').scrollTop=0;window.scrollTo(0,0);document.querySelector('$selector').scrollIntoView({block:'start'})");Thread.sleep(250);val raw=js("JSON.stringify((()=>{const el=document.querySelector('$selector');const r=el.getBoundingClientRect();const v=visualViewport;const body='$selector'==='.tiptap';const line=body?(el.querySelector('p')?.getBoundingClientRect()||r):r;return {x:(body?line.x+Math.min(12,line.width/2):r.x+r.width/2)-v.offsetLeft,y:(body?line.y+Math.min(12,line.height/2):r.y+r.height/2)-v.offsetTop,width:v.width,height:r.height}})())");val point=org.json.JSONObject(org.json.JSONTokener(raw).nextValue().toString());val location=IntArray(2);var width=0;val web=webView();compose.activityRule.scenario.onActivity{web.getLocationOnScreen(location);width=web.width};val ratio=width/point.getDouble("width");println("Web tap $point native=${location.toList()} width=$width ratio=$ratio");val x=(location[0]+point.getDouble("x")*ratio).toInt();val y=(location[1]+point.getDouble("y")*ratio).toInt();shell(if(longPress)"input swipe $x $y $x $y 900" else "input tap $x $y");if(selector==".tiptap"){compose.waitUntil(5000){js("Boolean(document.activeElement?.matches('.tiptap'))")=="true"};compose.waitForIdle();Thread.sleep(350)}}

    private fun hideKeyboard() { compose.activityRule.scenario.onActivity { androidx.core.view.WindowCompat.getInsetsController(it.window,it.window.decorView).hide(androidx.core.view.WindowInsetsCompat.Type.ime()) }; Thread.sleep(350); compose.waitForIdle() }
    private fun screenshot(name: String) { compose.waitForIdle(); Thread.sleep(500); val path="/sdcard/Download/moji-native-ui/$name.png"; shell("mkdir -p /sdcard/Download/moji-native-ui"); shell("screencap -p $path"); check((shell("stat -c %s $path").trim().toLongOrNull()?:0)>1024) }
    @Test fun startupPaintsTheEditorBeforeTheFirstRecordAndKeepsTheFullToolbar() {
        ready()
        compose.waitUntil(30000){compose.activity.editorSession.isPreloaded}
        assertNull(compose.activity.vm.state.value.selectedId)
        assertEquals("null",js("window.MojiEditor.recordId??null"))
        assertEquals("20",js("document.querySelectorAll('.tiptap-toolbar button').length"))
        compose.activityRule.scenario.onActivity{a->a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Text}.id);a.vm.addNote()}
        webReady()
        assertEquals("20",js("document.querySelectorAll('.tiptap-toolbar button').length"))
        assertEquals("false",js("document.querySelector('button[aria-label=加粗]').disabled"))
        assertEquals("false",js("document.activeElement===window.MojiEditor.editor.view.dom"))
        js("document.querySelector('button[aria-label=引用]').click()")
        compose.waitUntil(5000){js("Boolean(window.MojiEditor.editor.isActive('blockquote'))")=="true"}
        js("window.MojiEditor.deactivate()")
        assertEquals("20",js("document.querySelectorAll('.tiptap-toolbar button').length"))
        assertEquals("true",js("document.querySelector('button[aria-label=加粗]').disabled"))
        js("window.MojiEditor.activate()")
        assertEquals("false",js("document.querySelector('button[aria-label=加粗]').disabled"))
        screenshot("25-first-editor-full-toolbar")
    }
    @Test fun emptyWorkspaceCreatesOnlyTheNotebookChosenByUser() {
        ready(false); runBlocking { compose.activity.vm.replaceData(GameSaves.capture(Seed.create())) }
        compose.activityRule.scenario.onActivity { it.vm.navigate("home") }
        assertTrue(compose.activity.vm.state.value.workspace!!.notebooks.isEmpty())
        compose.onNodeWithTag("add-notebook-card").assertIsDisplayed(); screenshot("00-empty-workspace")
        compose.onAllNodesWithContentDescription("新建记录").assertCountEquals(0)
        compose.onNodeWithTag("add-notebook-card").performClick()
        compose.onNodeWithTag("notebook-name").performTextInput("My first notebook"); hideKeyboard()
        compose.onNodeWithText("账号密码",substring=false).performScrollTo().performClick()
        compose.onNodeWithText("创建记事本").performClick()
        val book=compose.activity.vm.state.value.workspace!!.notebooks.single()
        assertEquals("My first notebook",book.name); assertEquals(NoteMode.Password,book.mode)
        assertTrue(compose.activity.vm.state.value.workspace!!.notes.isEmpty())
        runBlocking { compose.activity.vm.flush() }; compose.activityRule.scenario.onActivity { it.vm.openLocal() }; ready(false)
        assertEquals(listOf(book),compose.activity.vm.state.value.workspace!!.notebooks)
    }
    @Test fun phoneTabletUltrawideAndRotationPreserveNativeEditing() {
        try {
            ready(); compose.activityRule.scenario.onActivity { it.vm.navigate("home") }; compose.onNodeWithTag("home-title").assertIsDisplayed(); screenshot("01-phone-home")
            compose.onNodeWithTag("book-Text").performClick()
            compose.activityRule.scenario.onActivity { it.vm.addNote() }
            compose.onNodeWithTag("note-title").performTextReplacement("Native layout test")
            webReady();js("window.MojiEditor.editor.commands.insertContent(\"Rotation and resize keep this text.\")")
            hideKeyboard(); val id=compose.activity.vm.state.value.selectedId!!
            compose.activityRule.scenario.onActivity { it.requestedOrientation=ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE }; Thread.sleep(1200)
            webReady();assertTrue(jsText().contains("Rotation and resize")); screenshot("02-phone-landscape")
            compose.activityRule.scenario.onActivity { it.requestedOrientation=ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED }
            resize(1600,2560,240); webReady(); screenshot("03-tablet-portrait")
            resize(2560,1600,240); compose.onNodeWithTag("note-list").assertExists(); screenshot("04-tablet-landscape")
            resize(3440,1440,240); webReady(); screenshot("05-wide-window")
            compose.onNode(hasText("工作台") and hasAnyAncestor(hasTestTag("wide-navigation"))).performClick(); screenshot("06-wide-home")
            compose.activityRule.scenario.onActivity { a -> val note=a.vm.state.value.workspace!!.notes.first{it.id==id}; a.vm.navigate(note.notebookId); a.vm.select(id) }
            resize(720,1280,320); webReady();assertTrue(jsText().contains("Rotation and resize")); screenshot("07-narrow-window")
            compose.activityRule.scenario.onActivity { it.onWindowFocusChanged(false) }; assertNotNull(compose.activity.vm.state.value.workspace)
        } catch(t:Throwable){runCatching{screenshot("failure-adaptive")};throw t} finally {shell("wm size reset");shell("wm density reset")}
    }
    @Test fun recordCreationExistsOnlyInsideANotebookAndCloudSavesRemainGlobal() {
        ready()
        val before=compose.activity.vm.state.value.workspace!!.notes.size
        listOf("home","all","star","archive","trash").forEach { view ->
            compose.activityRule.scenario.onActivity { it.vm.navigate(view) }
            compose.onAllNodesWithContentDescription("新建记录").assertCountEquals(0)
            compose.onAllNodesWithText("新建记录",substring=false).assertCountEquals(0)
        }
        assertEquals(before,compose.activity.vm.state.value.workspace!!.notes.size)
        val target=compose.activity.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Prompt}
        compose.activityRule.scenario.onActivity { it.vm.navigate(target.id) }
        compose.onNodeWithContentDescription("新建记录").performClick()
        assertEquals(target.id,compose.activity.vm.selectedNote()!!.notebookId); assertEquals("",compose.activity.vm.selectedNote()!!.content)
        compose.activityRule.scenario.onActivity { it.vm.navigate("home") }
        compose.onNodeWithContentDescription("打开导航").performClick(); compose.onNodeWithText("云存档").performClick()
        compose.onNodeWithTag("global-saves").assertIsDisplayed()
        listOf("save-create","save-overwrite","save-read","save-delete").forEach{compose.onNodeWithTag(it).assertExists()}; screenshot("09-global-saves")
    }
    @Test fun passwordNeedsExplicitEditAndLocalDataReopensWithoutPassword() {
        try {
            ready(); compose.activityRule.scenario.onActivity { a -> a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Password}.id); a.vm.addNote() }
            compose.onNodeWithText("完成").assertExists(); compose.onNodeWithTag("account").performTextInput("native-test@example.com"); compose.onNodeWithTag("credential-password").performTextInput("Test-only-2026!"); hideKeyboard()
            compose.onNodeWithTag("edit-credential").performClick(); compose.onNodeWithText("编辑密码").assertExists()
            compose.onNodeWithTag("account").assert(hasSetTextAction().not()); compose.onNodeWithTag("credential-password").assert(hasSetTextAction().not()); compose.onNodeWithContentDescription("复制账号").assertIsEnabled(); screenshot("08-native-password")
            val id=compose.activity.vm.selectedNote()!!.id
            compose.onNodeWithTag("edit-credential").performClick(); compose.onNodeWithTag("account").performTextReplacement("changed@example.com"); hideKeyboard(); compose.onNodeWithTag("edit-credential").performClick()
            runBlocking{compose.activity.vm.flush()}; assertTrue(compose.activity.vm.store.file.readText().contains("changed@example.com"))
            compose.activityRule.scenario.onActivity{it.vm.openLocal()}; ready(); assertEquals("changed@example.com",compose.activity.vm.state.value.workspace!!.notes.first{it.id==id}.username)
            val save=GameSaves.capture(compose.activity.vm.state.value.workspace!!)
            compose.activityRule.scenario.onActivity{a->a.vm.addNote(a.vm.state.value.workspace!!.liveBooks.first().id)}
            runBlocking{compose.activity.vm.replaceData(save)}; assertEquals(save.notes,compose.activity.vm.state.value.workspace!!.notes)
        }catch(t:Throwable){runCatching{screenshot("failure-persistence")};throw t}
    }
    @Test fun officialQuoteTasksVariablesAndLocalImagesPersist() {
        ready();compose.activityRule.scenario.onActivity{a->a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Prompt}.id);a.vm.addNote()};webReady()
        js("window.MojiEditor.editor.chain().focus().toggleBlockquote().insertContent('Quote').run()")
        focusWeb();InstrumentationRegistry.getInstrumentation().sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER);InstrumentationRegistry.getInstrumentation().sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER)
        InstrumentationRegistry.getInstrumentation().sendStringSync("Normal")
        compose.waitUntil(5000){jsText().contains("Normal")};assertTrue(org.json.JSONTokener(js("window.MojiEditor.editor.getHTML()")).nextValue().toString(),js("Boolean(window.MojiEditor.editor.state.doc.content.content.find(n=>n.type.name==='paragraph'&&n.textContent.includes('Normal')))")=="true")
        js("window.MojiEditor.editor.chain().clearContent().toggleTaskList().insertContent('Task').run()")
        focusWeb();InstrumentationRegistry.getInstrumentation().sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER);InstrumentationRegistry.getInstrumentation().sendStringSync("Second")
        js("document.querySelector('li input[type=checkbox]').click()")
        compose.waitUntil(5000){compose.activity.vm.selectedNote()!!.content.contains("data-checked=\"true\"")}
        focusWeb();InstrumentationRegistry.getInstrumentation().sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER);InstrumentationRegistry.getInstrumentation().sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER)
        InstrumentationRegistry.getInstrumentation().sendStringSync("Tail {{literal}}")
        compose.waitUntil(5000){jsText().contains("Tail {{literal}}") }
        js("const e=window.MojiEditor.editor;e.commands.insertContent('Audience');const end=e.state.selection.from;e.commands.setTextSelection({from:end-8,to:end})")
        compose.waitUntil(5000){compose.onAllNodes(hasText("转化为变量") and isEnabled()).fetchSemanticsNodes().size==1}
        compose.onNodeWithText("转化为变量").assertIsEnabled().performClick()
        compose.waitUntil(5000){js("Boolean(document.querySelector('.moji-variable'))")=="true"}
        InstrumentationRegistry.getInstrumentation().sendStringSync("Filled audience")
        compose.waitUntil(5000){PromptVariables.used(compose.activity.vm.selectedNote()!!).size==1};assertTrue(jsText().contains("{{literal}}"))
        js("window.MojiEditor.editor.chain().focus('end').setImage({src:'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZuoAAAAASUVORK5CYII='}).run()")
        compose.waitUntil(5000){compose.activity.vm.selectedNote()!!.content.contains("data:image/")};runBlocking{compose.activity.vm.flush()}
        val saved=compose.activity.vm.selectedNote()!!;screenshot("10-shared-editor")
        compose.activityRule.scenario.onActivity{it.vm.navigate("home")};compose.onNodeWithTag("home-title").assertExists();compose.activityRule.scenario.onActivity{it.vm.navigate(saved.notebookId);it.vm.select(saved.id)};webReady()
        assertTrue(jsText().contains("Filled audience"));assertTrue(js("Boolean(document.querySelector('.tiptap img')?.complete)")=="true");screenshot("11-explicit-variable")
    }
    @Test fun systemImagePickerReturnsToSameSharedDocument() {
        ready();compose.activityRule.scenario.onActivity{a->a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Text}.id);a.vm.addNote()};webReady()
        val context=compose.activity;val file=java.io.File(context.cacheDir,"exports/editor-test.png");file.parentFile!!.mkdirs();val bitmap=android.graphics.Bitmap.createBitmap(64,64,android.graphics.Bitmap.Config.ARGB_8888);bitmap.eraseColor(android.graphics.Color.GREEN);file.outputStream().use{bitmap.compress(android.graphics.Bitmap.CompressFormat.PNG,100,it)};bitmap.recycle()
        val uri=androidx.core.content.FileProvider.getUriForFile(context,context.packageName+".files",file)
        val instrumentation=InstrumentationRegistry.getInstrumentation()
        var pickerOpened=false
        val monitor=object:android.app.Instrumentation.ActivityMonitor(){override fun onStartActivity(intent:android.content.Intent):android.app.Instrumentation.ActivityResult? {println("Picker intent ${intent.action}");return if(intent.action in listOf(android.content.Intent.ACTION_OPEN_DOCUMENT,android.content.Intent.ACTION_GET_CONTENT)){pickerOpened=true;android.app.Instrumentation.ActivityResult(android.app.Activity.RESULT_OK,android.content.Intent().setData(uri).addFlags(android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION))}else null}}
        instrumentation.addMonitor(monitor)
        try {tapWeb("button[aria-label=\"插入图片\"]");compose.waitUntil(5000){pickerOpened};compose.waitUntil(15000){compose.activity.vm.selectedNote()!!.content.contains("data:image/")};assertEquals("true",js("Boolean(document.querySelector('.tiptap img')?.naturalWidth>0)"))}catch(t:Throwable){screenshot("failure-system-image");println(js("JSON.stringify({html:window.MojiEditor.editor.getHTML(),focus:document.activeElement?.outerHTML?.slice(0,200)})"));throw t}finally{instrumentation.removeMonitor(monitor);file.delete()}
    }
    @Test fun notebookAppearanceIsEditableAndSurvivesLocalAndGlobalSaves() {
        ready();compose.activityRule.scenario.onActivity{it.vm.navigate("home")}
        val original=compose.activity.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Prompt}
        compose.onNode(hasContentDescription("编辑记事本") and hasAnyAncestor(hasTestTag("book-Prompt"))).performClick()
        compose.onNodeWithText("编辑记事本").assertIsDisplayed()
        compose.onNodeWithTag("notebook-color-#A5849D").performScrollTo().performClick()
        compose.onNodeWithContentDescription("绿叶").performScrollTo().performClick()
        compose.onNodeWithText("渐变").performScrollTo().performClick();compose.onNodeWithTag("notebook-gradient-0").performScrollTo().performClick()
        hideKeyboard();screenshot("12-notebook-appearance")
        compose.onNodeWithText("保存修改").performClick()
        fun current()=compose.activity.vm.state.value.workspace!!.notebooks.first{it.id==original.id}
        assertEquals(original.mode,current().mode);assertEquals("#A1C4FD",current().color);assertEquals("#C2E9FB",current().colorEnd);assertEquals("Leaf",current().icon)
        runBlocking{compose.activity.vm.flush()};compose.activityRule.scenario.onActivity{it.vm.openLocal()};ready()
        assertEquals("#A1C4FD",current().color);assertEquals("#C2E9FB",current().colorEnd);assertEquals("Leaf",current().icon)
        val payload=GameSaves.capture(compose.activity.vm.state.value.workspace!!)
        compose.activityRule.scenario.onActivity{it.vm.saveBook(current(),original.name,original.mode,"#75876D","Cloud")}
        runBlocking{compose.activity.vm.replaceData(payload)}
        assertEquals("Leaf",current().icon);assertEquals("#A1C4FD",current().color);assertEquals("#C2E9FB",current().colorEnd);screenshot("13-custom-notebook-home")
    }
    @Test fun physicalKeyboardBurstPersistsEveryCharacter() {
        val original=shell("settings get secure show_ime_with_hard_keyboard").trim()
        try {
        for(shown in listOf("0","1")){
        shell("settings put secure show_ime_with_hard_keyboard $shown")
        ready();compose.activityRule.scenario.onActivity{a->a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Text}.id);a.vm.addNote()}
        val marker="MOJI_SIGNED_UPGRADE_PERSISTENCE_20261115"
        compose.waitUntil(10000){compose.activity.hasWindowFocus()}
        webReady();tapWeb(".tiptap");assertEquals("<p></p>",org.json.JSONTokener(js("MojiEditor.editor.getHTML()")).nextValue());InstrumentationRegistry.getInstrumentation().sendStringSync(marker)
        compose.waitUntil(5000){jsText().contains(marker)}
        val input=InstrumentationRegistry.getInstrumentation()
        input.sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_DEL)
        compose.waitUntil(5000){jsText()==marker.dropLast(1)}
        input.sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER);input.sendStringSync("Tail")
        compose.waitUntil(5000){js("Boolean(MojiEditor.editor.state.doc.content.content.some(n=>n.type.name==='paragraph'&&n.textContent==='Tail'))")=="true"}
        val expected=jsText()
        runBlocking{compose.activity.vm.flush()}
        val id=compose.activity.vm.selectedNote()!!.id;val book=compose.activity.vm.selectedNote()!!.notebookId
        compose.activityRule.scenario.onActivity{it.vm.navigate("home")};compose.onNodeWithTag("home-title").assertIsDisplayed()
        compose.activityRule.scenario.onActivity{it.vm.navigate(book);it.vm.select(id)};webReady()
        compose.waitUntil(5000){jsText()==expected}
        js("MojiEditor.editor.chain().clearContent().focus().toggleBlockquote().run()");focusWeb()
        input.sendStringSync("Quote");input.sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER);input.sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER);input.sendStringSync("Normal")
        compose.waitUntil(5000){js("Boolean(MojiEditor.editor.state.doc.content.content.some(n=>n.type.name==='paragraph'&&n.textContent==='Normal'))")=="true"}
        js("MojiEditor.editor.chain().clearContent().focus().toggleTaskList().run()");focusWeb()
        input.sendStringSync("Task");input.sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER);input.sendStringSync("Second");input.sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER);input.sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER);input.sendStringSync("Tail")
        compose.waitUntil(5000){js("Boolean(MojiEditor.editor.state.doc.content.content.some(n=>n.type.name==='paragraph'&&n.textContent==='Tail'))")=="true"}
        }
        }catch(t:Throwable){runCatching{println("Keyboard failure document: "+js("MojiEditor.editor.getHTML()"));screenshot("failure-physical-keyboard")};throw t}
        finally{shell(if(original=="null")"settings delete secure show_ime_with_hard_keyboard" else "settings put secure show_ime_with_hard_keyboard $original")}
    }
    @Test fun shortcutsCanBeChangedWithoutActivatingCommandsDuringCapture() {
        ready(); compose.activityRule.scenario.onActivity{it.vm.navigate("home")}
        compose.onNodeWithContentDescription("打开导航").performClick(); compose.onNodeWithContentDescription("偏好设置").performClick()
        compose.onNodeWithText("快捷键").performClick(); compose.onNodeWithTag("shortcut-new").performScrollTo().performClick()
        compose.onNodeWithTag("shortcut-capture").performKeyInput { keyDown(Key.CtrlLeft); keyDown(Key.ShiftLeft); pressKey(Key.N); keyUp(Key.ShiftLeft); keyUp(Key.CtrlLeft) }
        compose.onNodeWithText("保存").performClick()
        assertEquals("Ctrl+Shift+N",compose.activity.vm.state.value.workspace!!.preferences.shortcuts["new"])
        compose.onNodeWithContentDescription("关闭").performClick()
        compose.waitForIdle();compose.onNodeWithTag("adaptive-layout").performKeyInput { keyDown(Key.CtrlLeft); keyDown(Key.ShiftLeft); pressKey(Key.N); keyUp(Key.ShiftLeft); keyUp(Key.CtrlLeft) }
        compose.onNodeWithTag("home-title").assertIsDisplayed()
        compose.onAllNodesWithText("选择记事本").assertCountEquals(0)
        val book=compose.activity.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Text}
        val before=compose.activity.vm.state.value.workspace!!.notes.size
        compose.activityRule.scenario.onActivity { it.vm.navigate(book.id) }
        compose.waitForIdle();compose.onNodeWithTag("adaptive-layout").performKeyInput { keyDown(Key.CtrlLeft); keyDown(Key.ShiftLeft); pressKey(Key.N); keyUp(Key.ShiftLeft); keyUp(Key.CtrlLeft) }
        assertEquals(before+1,compose.activity.vm.state.value.workspace!!.notes.size)
        assertEquals(book.id,compose.activity.vm.selectedNote()!!.notebookId)
    }
    @Test fun drawerDismissesEditorHandlesAndFeedbackUsesTheForegroundWindow() {
        try {
            ready(); compose.activityRule.scenario.onActivity { a -> a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first { it.mode==NoteMode.Text }.id); a.vm.addNote() }
            webReady(); js("window.MojiEditor.editor.commands.insertContent('Selection remains editable after navigation')")
            tapWeb(".tiptap")
            tapWeb(".tiptap p",longPress=true)
            compose.waitUntil(5000) { js("Boolean(window.getSelection()?.toString().length)")=="true" }
            val body=jsText()
            screenshot("14-editor-selection")
            compose.onNodeWithTag("adaptive-layout").performTouchInput {
                swipe(start=androidx.compose.ui.geometry.Offset(1f,height*.55f),end=androidx.compose.ui.geometry.Offset(width*.85f,height*.55f),durationMillis=650)
            }
            compose.onNodeWithContentDescription("偏好设置").assertIsDisplayed()
            compose.waitUntil(5000) { !webView().hasFocus() }
            compose.activityRule.scenario.onActivity { it.vm.message("侧栏消息可见") }
            compose.onNodeWithText("侧栏消息可见").assertIsDisplayed()
            screenshot("15-drawer-feedback-no-handles")
            compose.onNodeWithContentDescription("偏好设置").performClick()
            compose.onNodeWithText("偏好设置",substring=false).assertIsDisplayed()
            compose.onNodeWithText("更新",substring=false).performScrollTo().performClick()
            compose.activityRule.scenario.onActivity { it.vm.message("你已在使用最新版本") }
            compose.waitUntil(8000) { compose.onAllNodesWithText("你已在使用最新版本").fetchSemanticsNodes().isNotEmpty() }
            compose.onNode(hasText("你已在使用最新版本") and hasAnyAncestor(isDialog())).assertIsDisplayed()
            screenshot("16-update-feedback-above-dialog")
            compose.onNodeWithContentDescription("关闭").performClick()
            compose.waitForIdle()
            webReady(); assertEquals(body,jsText())
            tapWeb(".tiptap"); js("window.MojiEditor.editor.commands.insertContent(' Still editable')")
            compose.waitUntil(5000) { jsText().contains("Still editable") }
        } catch(t:Throwable) { runCatching { screenshot("failure-overlay-layers") }; throw t }
    }
    @Test fun titleEnterAndTabFocusTheBodyAndSwitchingRecordsReusesOnePage() {
        ready();compose.activityRule.scenario.onActivity{a->a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Text}.id);a.vm.addNote()};webReady()
        val original=webView();val first=compose.activity.vm.selectedNote()!!.id;val book=compose.activity.vm.selectedNote()!!.notebookId
        js("window.__mojiReuseMarker='same-page'")
        compose.onNodeWithTag("note-title").performTextReplacement("Title hop")
        InstrumentationRegistry.getInstrumentation().sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_ENTER)
        compose.waitUntil(5000){js("document.activeElement===window.MojiEditor.editor.view.dom")=="true"}
        // DOM focus precedes the native IME handoff. Let that handoff settle
        // before injecting a full hardware-key string, as tapWeb does.
        compose.waitForIdle();InstrumentationRegistry.getInstrumentation().waitForIdleSync();Thread.sleep(350)
        InstrumentationRegistry.getInstrumentation().sendStringSync("First content")
        compose.waitUntil(5000){jsText().contains("First content")};assertEquals("Title hop",compose.activity.vm.selectedNote()!!.title)
        compose.onNodeWithTag("note-title").performClick();InstrumentationRegistry.getInstrumentation().sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_TAB)
        compose.waitUntil(5000){js("document.activeElement===window.MojiEditor.editor.view.dom")=="true"}
        hideKeyboard();compose.activityRule.scenario.onActivity{it.vm.addNote()};webReady();assertSame(original,webView());assertEquals("same-page",org.json.JSONTokener(js("window.__mojiReuseMarker")).nextValue())
        js("window.MojiEditor.editor.commands.insertContent('Second content')")
        compose.activityRule.scenario.onActivity{it.vm.navigate("home")};compose.onNodeWithTag("home-title").assertIsDisplayed()
        compose.activityRule.scenario.onActivity{it.vm.navigate(book);it.vm.select(first)};webReady();assertSame(original,webView());assertEquals("First content",jsText());assertFalse(js("window.MojiEditor.editor.can().undo()")=="true")
    }
    @Test fun emptyTrashClearsHiddenRecordsAndLeavesLiveRecordsAlone() {
        ready();val book=compose.activity.vm.state.value.workspace!!.liveBooks.first().id
        val live=Note(notebookId=book,title="Keep me",content="Live")
        val a=Note(notebookId=book,title="Match",content="Deleted A",deletedAt=now())
        val b=Note(notebookId=book,title="Hidden",password="Fake",content="Deleted B",deletedAt=now())
        compose.activityRule.scenario.onActivity{it.vm.mutate{w->w.copy(notes=w.notes+listOf(live,a,b))};it.vm.navigate("trash");it.vm.search("Match")}
        compose.onNodeWithTag("empty-trash").assertIsEnabled().performClick();compose.onNodeWithText("确认",substring=false).performClick()
        compose.waitUntil(5000){compose.activity.vm.state.value.workspace!!.notes.none{it.deletedAt!=null&&it.purgedAt==null}}
        val notes=compose.activity.vm.state.value.workspace!!.notes;assertEquals("Live",notes.first{it.id==live.id}.content);assertEquals("",notes.first{it.id==b.id}.password)
    }

    @Test fun credentialAttributesArePerRecordReadOnlyUntilEditAndSurviveReopen() {
        ready();compose.activityRule.scenario.onActivity{a->a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Password}.id);a.vm.addNote()}
        compose.onNodeWithTag("new-credential-field-name").performScrollTo().performTextInput("账户地区");hideKeyboard();compose.onNodeWithTag("add-credential-field").performClick()
        val field=compose.activity.vm.selectedNote()!!.fields.single();compose.onNodeWithTag("credential-field-${field.id}").performScrollTo().performTextInput("新加坡");hideKeyboard();compose.onNodeWithTag("edit-credential").performClick()
        compose.onNodeWithTag("credential-field-${field.id}").performScrollTo().assert(hasSetTextAction().not());compose.onNodeWithContentDescription("复制账户地区").assertIsEnabled();val id=compose.activity.vm.selectedNote()!!.id
        runBlocking{compose.activity.vm.flush()};compose.activityRule.scenario.onActivity{it.vm.openLocal()};ready();val saved=compose.activity.vm.state.value.workspace!!.notes.first{it.id==id};assertEquals("新加坡",saved.fields.single().value);assertEquals(2,compose.activity.vm.state.value.workspace!!.schema)
        compose.activityRule.scenario.onActivity{it.vm.navigate(saved.notebookId);it.vm.select(id)};compose.onNodeWithTag("credential-field-${field.id}").performScrollTo().assert(hasSetTextAction().not());screenshot("27-custom-password-attributes")
    }
    @Test fun trashUsesIndependentCardsAndCheckboxesRestoreSelectedRecords() {
        ready();val book=compose.activity.vm.state.value.workspace!!.liveBooks.first().id;val a=Note(notebookId=book,title="Restore A",deletedAt=now()+3);val b=Note(notebookId=book,title="Restore B",deletedAt=now()+2);val c=Note(notebookId=book,title="Leave C",deletedAt=now()+1)
        compose.activityRule.scenario.onActivity{it.vm.mutate{w->w.copy(notes=w.notes+listOf(a,b,c))};it.vm.navigate("trash")}
        compose.onNodeWithTag("trash-page").assertIsDisplayed();compose.onAllNodesWithTag("shared-editor").assertCountEquals(0)
        compose.onNodeWithTag("trash-select-${a.id}").performScrollTo().performClick();compose.onNodeWithTag("trash-select-${b.id}").performScrollTo().performClick();compose.onNodeWithTag("restore-trash").assertIsEnabled();screenshot("28-trash-card-selection");compose.onNodeWithTag("restore-trash").performClick()
        compose.waitUntil(5000){compose.activity.vm.state.value.workspace!!.notes.first{it.id==a.id}.deletedAt==null};assertNull(compose.activity.vm.state.value.workspace!!.notes.first{it.id==b.id}.deletedAt);assertNotNull(compose.activity.vm.state.value.workspace!!.notes.first{it.id==c.id}.deletedAt)
    }
    @Test fun notebooksDragInDashboardAndSidebarAndPersistOneOrder() {
        try{
            resize(720,1600,320);ready(false);val a=Notebook(name="First",mode=NoteMode.Text,color="#C06C68");val b=Notebook(name="Second",mode=NoteMode.Prompt,color="#6D89A6");val c=Notebook(name="Third",mode=NoteMode.Password,color="#6F8B67")
            runBlocking{compose.activity.vm.replaceData(GameSaves.capture(Workspace(notebooks=listOf(a,b,c))))};compose.activityRule.scenario.onActivity{it.vm.navigate("home")};compose.waitForIdle()
            fun coverPixel():Int{val pixels=compose.onNodeWithTag("book-Text").captureToImage().toPixelMap();return pixels[(pixels.width*.64f).toInt(),(pixels.height*.82f).toInt()].toArgb()}
            val lightCover=coverPixel();compose.activityRule.scenario.onActivity{it.vm.preferences{p->p.copy(theme="dark")}};compose.waitForIdle();assertEquals(lightCover,coverPixel());compose.activityRule.scenario.onActivity{it.vm.preferences{p->p.copy(theme="light")}};compose.waitForIdle();screenshot("40-opaque-cover-consistent")
            val sourceBounds=compose.onNodeWithTag("book-Text").fetchSemanticsNode().boundsInRoot;val targetBounds=compose.onNodeWithTag("book-Prompt").fetchSemanticsNode().boundsInRoot
            compose.onNodeWithTag("book-Text").performTouchInput{down(center);advanceEventTime(700);moveTo(androidx.compose.ui.geometry.Offset(targetBounds.center.x-sourceBounds.left,targetBounds.center.y-sourceBounds.top),600)};compose.waitForIdle();assertEquals(listOf(a.id,b.id,c.id),compose.activity.vm.state.value.workspace!!.liveBooks.map{it.id});compose.onNodeWithTag("book-Text").performTouchInput{cancel()};compose.waitForIdle();assertEquals(listOf(a.id,b.id,c.id),compose.activity.vm.state.value.workspace!!.liveBooks.map{it.id});assertEquals(sourceBounds,compose.onNodeWithTag("book-Text").fetchSemanticsNode().boundsInRoot)
            fun drag(sourceTag:String,targetTag:String){val saved=compose.activity.vm.state.value.workspace!!.liveBooks.map{it.id};val source=compose.onNodeWithTag(sourceTag).fetchSemanticsNode().boundsInRoot;val target=compose.onNodeWithTag(targetTag).fetchSemanticsNode().boundsInRoot;compose.onNodeWithTag(sourceTag).performTouchInput{down(center);advanceEventTime(700);moveTo(androidx.compose.ui.geometry.Offset(target.center.x-source.left,target.center.y-source.top),600)};compose.waitForIdle();assertEquals(saved,compose.activity.vm.state.value.workspace!!.liveBooks.map{it.id});compose.onAllNodesWithTag(sourceTag).assertCountEquals(1);compose.onNodeWithTag(sourceTag).performTouchInput{up()};compose.waitForIdle()}
            drag("book-Text","book-Prompt");compose.waitUntil(5000){compose.activity.vm.state.value.workspace!!.liveBooks.first().id==b.id};screenshot("29-notebook-drag-dashboard")
            compose.onNodeWithContentDescription("打开导航").performClick();compose.onNodeWithTag("notebook-sidebar-list").performScrollToKey(b.id);drag("sidebar-book-${b.id}","sidebar-book-${a.id}");compose.waitUntil(5000){compose.activity.vm.state.value.workspace!!.liveBooks.first().id==a.id};screenshot("30-notebook-drag-sidebar")
            runBlocking{compose.activity.vm.flush()};compose.activityRule.scenario.onActivity{it.vm.openLocal()};ready(false);assertEquals(listOf(a.id,b.id,c.id),compose.activity.vm.state.value.workspace!!.liveBooks.map{it.id})
        }finally{shell("wm size reset");shell("wm density reset")}
    }

    @Test fun sixThemesUpdateTheNativeSettingsAndExistingEditorAndPersistChoices(){
        try{
            resize(1440,1080,160);ready();val original=compose.activity.vm.state.value.workspace!!.preferences
            compose.activityRule.scenario.onActivity{a->a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Text}.id);a.vm.addNote()};webReady();js("window.MojiEditor.editor.commands.insertContent('Theme document remains unchanged')");val content=jsText();val native=webView()
            compose.onNode(hasContentDescription("偏好设置") and hasAnyAncestor(hasTestTag("wide-navigation"))).performClick()
            for(palette in ThemeCatalog.all){compose.onNodeWithTag("theme-${palette.id}").performScrollTo().performClick();try{compose.waitUntil(5000){org.json.JSONTokener(js("document.documentElement.dataset.palette")).nextValue()==palette.id}}catch(t:Throwable){println("Theme mismatch wanted=${palette.id} actual="+js("document.documentElement.dataset.palette"));screenshot("failure-theme-${palette.id}");throw t};assertEquals(content,jsText());assertSame(native,webView());screenshot("theme-${palette.id}")}
            compose.onNodeWithContentDescription("关闭").performClick();webReady();runBlocking{compose.activity.vm.flush()};compose.activityRule.scenario.onActivity{it.vm.openLocal()};ready(false);val prefs=compose.activity.vm.state.value.workspace!!.preferences;assertEquals("celadon",prefs.lightPalette);assertEquals("dune",prefs.darkPalette);compose.activityRule.scenario.onActivity{it.vm.preferences{original}}
        }finally{shell("wm size reset");shell("wm density reset")}
    }
    @Test fun cardPinPreservesSelectionAndEditTimeAndDuplicateCopiesFullIndependentRecord(){
        try{
            resize(1440,1080,160);ready(false);val book=Notebook(name="Record actions",mode=NoteMode.Text);val old=Note(notebookId=book.id,title="Older",content="Original body",updatedAt=1,createdAt=1,fields=listOf(CredentialField(name="Custom",value="Value")));val active=Note(notebookId=book.id,title="Active",content="Active body")
            runBlocking{compose.activity.vm.replaceData(GameSaves.capture(Workspace(notebooks=listOf(book),notes=listOf(old,active))))};compose.activityRule.scenario.onActivity{it.vm.navigate(book.id);it.vm.select(active.id)};webReady();val native=webView()
            compose.onNodeWithTag("pin-record-${old.id}").performClick();compose.waitUntil(5000){compose.activity.vm.filtered().first().id==old.id};assertEquals(active.id,compose.activity.vm.selectedNote()!!.id);assertSame(native,webView());assertEquals("Active body",jsText());assertEquals(1L,compose.activity.vm.state.value.workspace!!.notes.first{it.id==old.id}.updatedAt)
            for(sort in listOf("name","created","updated")){compose.activityRule.scenario.onActivity{it.vm.sort(sort)};assertEquals(old.id,compose.activity.vm.filtered().first().id)}
            screenshot("31-card-pin-only");compose.onAllNodesWithTag("duplicate-record-${old.id}").assertCountEquals(0);compose.onNodeWithTag("record-card-${old.id}").performMouseInput{click(button=MouseButton.Secondary)};compose.onNodeWithText("创建副本").performClick();webReady();val copy=compose.activity.vm.selectedNote()!!;assertNotEquals(old.id,copy.id);assertEquals(book.id,copy.notebookId);assertFalse(copy.pinned);assertTrue(copy.history.isEmpty());assertEquals(old.fields,copy.fields);assertEquals("Original body",jsText());compose.activityRule.scenario.onActivity{it.vm.edit(copy.id){it.copy(fields=it.fields.map{f->f.copy(value="New")})}};assertEquals("Value",compose.activity.vm.state.value.workspace!!.notes.first{it.id==old.id}.fields.single().value)
            runBlocking{compose.activity.vm.flush()};compose.activityRule.scenario.onActivity{it.vm.openLocal()};ready(false);assertTrue(compose.activity.vm.state.value.workspace!!.notes.first{it.id==old.id}.pinned)
        }finally{shell("wm size reset");shell("wm density reset")}
    }

    @Test fun imageBackgroundControlsCanBeTappedAndReopenWithTheRecord(){
        ready();compose.activityRule.scenario.onActivity{a->a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Text}.id);a.vm.addNote()};webReady();val note=compose.activity.vm.selectedNote()!!
        js("window.MojiEditor.editor.commands.setContent('<img width=160 alt=Background src=data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAKAAAABkCAYAAAABtjuPAAABeElEQVR4nO3RQQ0CABDEQBRjCDk4QBEW+JBesjNJFfTxAAAAAAAAAAAAAAAAAAAAAOBHz9f7o/9WPz6tnrNQ/fi0es5C9ePT6jkL1Y9Pq+csVD8+rZ6zUP34tHrOQvXj0+o5C9WPT6vnLFQ/Pq2es1D9+LR6zkL149PqOQvVj0+r5yxUPz6tnrNQ/fi0es5C9ePT6jkL1Y9Pq+csVD8+rZ6zUP34tHrOQvXj0+o5C9WPT6vnLFQ/Pq2es1D9+LR6zkL149PqOQvVj0+r5yxUPz6tnrNQ/fi0es5C9ePT6jkL1Y9Pq+csVD8+rZ6zUP34tHrOQvXj0+o5C9WPT6vnLFQ/Pq2es1D9+LR6zkL149PqOQvVj0+r5yxUPz6tnrNQ/fi0es5C9ePT6jkL1Y9Pq+csVD8+rZ6zUP34tHrOQvXj0+o5C9WPT6vnLFQ/Pq2es1D9+LR6zkL149PqOQvVj0+r5yxUPwYAAAAAAAAAAAAAAAAAAICffQGkmZgjyMdk4wAAAABJRU5ErkJggg==><p></p>');window.MojiEditor.editor.commands.setNodeSelection(0)");tapWeb(".tiptap img");compose.waitUntil(5000){js("getComputedStyle(document.querySelector('.moji-image-backgrounds')).visibility")=="\"visible\""};tapWeb("button[aria-label=透明网格]");compose.waitUntil(5000){compose.activity.vm.selectedNote()!!.content.contains("data-moji-image-background=\"checker\"")};screenshot("32-image-checker-background");runBlocking{compose.activity.vm.flush()};compose.activityRule.scenario.onActivity{it.vm.navigate("home");it.vm.navigate(note.notebookId);it.vm.select(note.id)};webReady();assertEquals("checker",org.json.JSONTokener(js("document.querySelector('.tiptap img').dataset.mojiImageBackground")).nextValue())
    }

    @Test fun installedDisplayFontSearchPreviewApplyAndReopenUseTheSameEditor(){
        try{
            resize(1440,1080,160);ready();compose.activityRule.scenario.onActivity{a->a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Text}.id);a.vm.addNote()};webReady();js("window.MojiEditor.editor.commands.insertContent('Display font retained')");compose.waitUntil(5000){compose.activity.vm.selectedNote()!!.content.contains("Display font retained")};val native=webView();val note=compose.activity.vm.selectedNote()!!
            val font=InstalledFonts.all.firstOrNull{it.id=="serif"&&it.index==0}?:InstalledFonts.all.first{it.id.isNotEmpty()&&it.file!=null&&it.index==0};assertNotNull(InstalledFonts.typeface(font.id))
            compose.onNode(hasContentDescription("偏好设置") and hasAnyAncestor(hasTestTag("wide-navigation"))).performClick();compose.onNodeWithTag("choose-display-font").performScrollTo().performClick();compose.onNodeWithTag("font-search").performTextInput(font.name);hideKeyboard();compose.onNodeWithTag("installed-font-list").performScrollToKey(font.id);compose.onNodeWithTag("font-${font.id}").performClick();screenshot("33-font-picker");compose.onNodeWithTag("apply-display-font").assertIsEnabled().performClick()
            compose.waitUntil(10000){compose.activity.vm.state.value.workspace!!.preferences.fontFamily==font.id&&js("getComputedStyle(document.querySelector('.tiptap')).fontFamily").contains("Moji Display")};js("window.__fontLoaded=false;document.fonts.load('16px \"Moji Display\"').then(fonts=>window.__fontLoaded=fonts.length>0)");compose.waitUntil(10000){js("window.__fontLoaded")=="true"};assertSame(native,webView());assertEquals("Display font retained",jsText());assertEquals(note.content,compose.activity.vm.selectedNote()!!.content)
            compose.onNodeWithContentDescription("关闭").performClick();webReady();runBlocking{compose.activity.vm.flush()};compose.activityRule.scenario.onActivity{it.vm.openLocal()};ready(false);assertEquals(font.id,compose.activity.vm.state.value.workspace!!.preferences.fontFamily)
        }finally{shell("wm size reset");shell("wm density reset")}
    }
    @Test fun explicitRecordSelectionKeepsTheActiveBodyAndAllowsEmptyChecksAndCancel(){
        try{
            resize(1440,1080,160);ready(false);val book=Notebook(name="Select records");val active=Note(notebookId=book.id,title="Active",content="Keep body");val other=Note(notebookId=book.id,title="Other",content="Other body")
            runBlocking{compose.activity.vm.replaceData(GameSaves.capture(Workspace(notebooks=listOf(book),notes=listOf(active,other))))};compose.activityRule.scenario.onActivity{it.vm.navigate(book.id);it.vm.select(active.id)};webReady();val native=webView()
            compose.onNodeWithTag("select-records").performClick();compose.onNodeWithTag("delete-selected-records").assertIsNotEnabled();compose.onNodeWithTag("select-all-records").performClick();compose.onNodeWithTag("select-record-${other.id}").assertIsOn();compose.onNodeWithTag("select-record-${active.id}").assertIsOn();assertEquals(active.id,compose.activity.vm.selectedNote()!!.id);assertSame(native,webView());assertEquals("Keep body",jsText());screenshot("34-record-selection")
            compose.onNodeWithTag("select-all-records").performClick();compose.onNodeWithTag("delete-selected-records").assertIsNotEnabled();compose.onNodeWithTag("cancel-record-selection").assertIsDisplayed();compose.onNodeWithTag("select-record-${other.id}").performClick();compose.onNodeWithTag("delete-selected-records").performClick();compose.waitUntil(5000){compose.activity.vm.state.value.workspace!!.notes.first{it.id==other.id}.deletedAt!=null};assertNull(compose.activity.vm.state.value.workspace!!.notes.first{it.id==active.id}.deletedAt);assertEquals(active.id,compose.activity.vm.selectedNote()!!.id);compose.onNodeWithTag("select-records").performClick();compose.onNodeWithTag("cancel-record-selection").performClick();compose.onNodeWithTag("select-records").assertIsDisplayed()
        }finally{shell("wm size reset");shell("wm density reset")}
    }

    @Test fun inlineGlobalSearchKeepsTypingInPlaceOpensOtherNotebooksAndWorksInThePhoneDrawer(){
        val original=shell("settings get secure show_ime_with_hard_keyboard").trim()
        try{
            shell("settings put secure show_ime_with_hard_keyboard 1");resize(1440,700,160);ready(false)
            val currentBook=Notebook(name="Inbox",mode=NoteMode.Prompt);val current=Note(notebookId=currentBook.id,title="Current",content="Current body");val textBook=Notebook(name="Research",mode=NoteMode.Text);val passwordBook=Notebook(name="Accounts",mode=NoteMode.Password);val text=Note(notebookId=textBook.id,title="Needle plan",content="Needle body");val password=Note(notebookId=passwordBook.id,title="Needle service",username="demo",password="must-not-appear-in-results")
            runBlocking{compose.activity.vm.replaceData(GameSaves.capture(Workspace(notebooks=listOf(currentBook,textBook,passwordBook),notes=listOf(current,text,password))))};compose.activityRule.scenario.onActivity{it.vm.navigate(currentBook.id);it.vm.select(current.id)};webReady();val native=webView()
            val search=compose.onNode(hasTestTag("global-search") and hasAnyAncestor(hasTestTag("wide-navigation")));search.performClick();search.performTextInput("Needle")
            compose.waitUntil(10000){compose.onAllNodesWithTag("global-result-${text.id}").fetchSemanticsNodes().size==1};compose.onAllNodes(isDialog()).assertCountEquals(0);compose.onAllNodes(hasText("must-not-appear-in-results") and hasAnyAncestor(hasTestTag("global-search-results"))).assertCountEquals(0);compose.onNodeWithTag("wide-navigation").assertIsDisplayed();assertEquals(current.id,compose.activity.vm.selectedNote()!!.id);screenshot("35-global-search-wide")
            compose.onNodeWithTag("global-result-${text.id}").performClick();webReady();assertEquals(textBook.id,compose.activity.vm.state.value.view);assertEquals(text.id,compose.activity.vm.selectedNote()!!.id);assertEquals("Needle body",jsText());assertSame(native,webView())
            search.performClick();search.performTextReplacement("no-such-search-result");compose.waitUntil(10000){compose.onAllNodesWithText("没有找到记录").fetchSemanticsNodes().isNotEmpty()};compose.activityRule.scenario.onActivity{it.onBackPressedDispatcher.onBackPressed()};compose.waitForIdle();compose.onAllNodesWithTag("global-search-results").assertCountEquals(0);assertEquals(text.id,compose.activity.vm.selectedNote()!!.id)
            hideKeyboard();resize(720,1600,320);compose.activityRule.scenario.onActivity{it.vm.navigate("home")};compose.onNodeWithContentDescription("全局搜索").performClick();compose.waitUntil(10000){compose.onAllNodesWithTag("global-search-results").fetchSemanticsNodes().isNotEmpty()};compose.onAllNodes(isDialog()).assertCountEquals(0);screenshot("36-global-search-phone")
        }finally{shell(if(original=="null")"settings delete secure show_ime_with_hard_keyboard"else"settings put secure show_ime_with_hard_keyboard $original");shell("wm size reset");shell("wm density reset")}
    }

    private fun sidebarBook(id:String)=compose.onNode(hasTestTag("sidebar-book-$id") and SemanticsMatcher("onscreen notebook"){it.boundsInRoot.right>0&&it.boundsInRoot.left>=0})
    private fun recordChord(key:Key){compose.onRoot().performKeyInput{keyDown(Key.CtrlLeft);pressKey(key);keyUp(Key.CtrlLeft)};compose.waitForIdle()}
    private fun clipboardToken():String?=compose.activity.getSystemService(android.content.ClipboardManager::class.java).primaryClipDescription?.extras?.getString("moji-record-token")
    @Test fun recordClipboardCopiesCutsAtomicallyAndTextSelectionKeepsItsOwnShortcuts(){
        try{
            resize(1440,1080,160);ready(false)
            val a=Notebook(name="Copy source",mode=NoteMode.Password);val b=Notebook(name="Copy target",mode=NoteMode.Password);val wrong=Notebook(name="Text target",mode=NoteMode.Text)
            val n=Note(notebookId=a.id,title="First account",password="test-only",updatedAt=10,fields=listOf(CredentialField(name="Region",value="Stored")));val n2=Note(notebookId=a.id,title="Second account",updatedAt=11)
            runBlocking{compose.activity.vm.replaceData(GameSaves.capture(Workspace(notebooks=listOf(a,b,wrong),notes=listOf(n,n2))))};compose.activityRule.scenario.onActivity{it.vm.navigate(a.id)}
            compose.onNodeWithTag("record-card-${n.id}").performClick();recordChord(Key.C);compose.waitUntil(8000){compose.activity.vm.state.value.recordClipboardReady}
            sidebarBook(b.id).performClick();recordChord(Key.V);compose.waitUntil(8000){compose.activity.vm.state.value.workspace!!.liveNotes.any{it.notebookId==b.id}}
            val copy=compose.activity.vm.state.value.workspace!!.liveNotes.first{it.notebookId==b.id};assertNotEquals(n.id,copy.id);assertEquals(n.fields,copy.fields);assertEquals(n.password,copy.password)
            sidebarBook(a.id).performClick();compose.onNodeWithTag("select-records").performClick();compose.onNodeWithTag("select-all-records").performClick();val oldToken=clipboardToken();recordChord(Key.X);compose.waitUntil(8000){clipboardToken()!=oldToken};assertEquals(a.id,compose.activity.vm.state.value.workspace!!.notes.first{it.id==n.id}.notebookId)
            sidebarBook(wrong.id).performClick();recordChord(Key.V);compose.waitUntil(8000){compose.onAllNodesWithText("记事本类型不兼容，未移动任何记录。").fetchSemanticsNodes().isNotEmpty()};assertTrue(compose.activity.vm.state.value.workspace!!.notes.filter{it.id in setOf(n.id,n2.id)}.all{it.notebookId==a.id})
            sidebarBook(b.id).performClick();recordChord(Key.V);compose.waitUntil(8000){compose.activity.vm.state.value.workspace!!.notes.first{it.id==n.id}.notebookId==b.id};assertTrue(compose.activity.vm.state.value.workspace!!.notes.filter{it.id in setOf(n.id,n2.id)}.all{it.notebookId==b.id});assertEquals(10L,compose.activity.vm.state.value.workspace!!.notes.first{it.id==n.id}.updatedAt)
            compose.onNodeWithTag("record-card-${n.id}").performClick();recordChord(Key.C);compose.waitUntil(8000){compose.activity.vm.state.value.recordClipboardReady};compose.onNodeWithTag("credential-password").performClick();compose.onNodeWithTag("credential-password").performTextInputSelection(androidx.compose.ui.text.TextRange(0,n.password.length));recordChord(Key.C)
            compose.waitUntil(8000){compose.activity.getSystemService(android.content.ClipboardManager::class.java).primaryClip?.getItemAt(0)?.text?.toString()==n.password};assertFalse(compose.activity.vm.hasRecordClipboard());screenshot("37-record-clipboard-and-text-selection")
        }finally{shell("wm size reset");shell("wm density reset")}
    }
    private fun recordBounds(tag:String):android.graphics.Rect?{
        // Accessibility caches the closed drawer while a platform drag is active.
        // Read the actual Compose layout; input/drop still go through Android.
        val root=compose.onRoot().fetchSemanticsNode().boundsInRoot
        val bounds=compose.onAllNodesWithTag(tag).fetchSemanticsNodes().map{it.boundsInRoot}.firstOrNull{
            it.left>=0&&it.right>0&&it.right<=root.width&&it.top>=0&&it.bottom<=root.height
        }?:return null
        val offset=IntArray(2);compose.activityRule.scenario.onActivity{it.window.decorView.getLocationOnScreen(offset)}
        return android.graphics.Rect(bounds.left.toInt()+offset[0],bounds.top.toInt()+offset[1],bounds.right.toInt()+offset[0],bounds.bottom.toInt()+offset[1])
    }
    private fun nativeRecordDrag(source:String,target:String){
        // Selection inserts a toolbar and moves the cards. Do not inject a native
        // down into the old accessibility bounds before Compose finishes layout.
        compose.waitForIdle()
        val origin=recordBounds(source)?:error("Record drag source has no screen bounds")
        val automation=InstrumentationRegistry.getInstrumentation().uiAutomation
        val downTime=android.os.SystemClock.uptimeMillis()
        val events=java.util.concurrent.ConcurrentLinkedQueue<Boolean>()
        lateinit var observer:kotlinx.coroutines.Job
        compose.activityRule.scenario.onActivity{a->observer=a.lifecycleScope.launch{a.vm.recordDrags.collect{events.add(it)}}}
        fun input(action:Int,x:Int,y:Int){
            val event=android.view.MotionEvent.obtain(downTime,android.os.SystemClock.uptimeMillis(),action,x.toFloat(),y.toFloat(),0)
            event.source=android.view.InputDevice.SOURCE_TOUCHSCREEN
            try{check(automation.injectInputEvent(event,true)){"Native drag input rejected: $action"}}finally{event.recycle()}
        }
        try{
            input(android.view.MotionEvent.ACTION_DOWN,origin.centerX(),origin.centerY());Thread.sleep(850)
            compose.waitUntil(8000){true in events}
            assertEquals("Platform drag must retain both selected records",2,compose.activity.vm.recordsForCommand().size)
            compose.waitUntil(8000){recordBounds(target)!=null};val goal=recordBounds(target)!!
            input(android.view.MotionEvent.ACTION_MOVE,goal.centerX(),goal.centerY());Thread.sleep(250);screenshot("38-record-drag-target")
            input(android.view.MotionEvent.ACTION_UP,goal.centerX(),goal.centerY());compose.waitForIdle()
        }catch(failure:Throwable){
            println("Record drag failed: origin=$origin events=$events selected=${compose.activity.vm.recordsForCommand()} sourceNow=${recordBounds(source)} targetNow=${recordBounds(target)}")
            runCatching{screenshot("failure-record-drag")};compose.onRoot().printToLog("MojiRecordDrag");throw failure
        }finally{runCatching{input(android.view.MotionEvent.ACTION_UP,1,1)};observer.cancel()}
    }
    @Test fun selectedRecordsLongPressDragIntoSidebarAndRejectWrongMode(){
        try{
            resize(720,1600,320);ready(false)
            val a=Notebook(name="Drag source",mode=NoteMode.Text);val b=Notebook(name="Drag target",mode=NoteMode.Text);val wrong=Notebook(name="Passwords",mode=NoteMode.Password)
            val n=Note(notebookId=a.id,title="Drag one",content="One",updatedAt=10);val n2=Note(notebookId=a.id,title="Drag two",content="Two",updatedAt=11)
            runBlocking{compose.activity.vm.replaceData(GameSaves.capture(Workspace(notebooks=listOf(a,b,wrong),notes=listOf(n,n2))))};compose.activityRule.scenario.onActivity{it.vm.navigate(a.id)}
            compose.onNodeWithTag("select-records").performClick();compose.onNodeWithTag("select-all-records").performClick();nativeRecordDrag("record-card-${n.id}","sidebar-book-${b.id}")
            compose.waitUntil(8000){compose.activity.vm.state.value.workspace!!.notes.filter{it.id in setOf(n.id,n2.id)}.all{it.notebookId==b.id}};assertEquals(10L,compose.activity.vm.state.value.workspace!!.notes.first{it.id==n.id}.updatedAt);webReady();assertEquals("false",js("['c','x','v'].some(key=>{const e=new KeyboardEvent('keydown',{key,ctrlKey:true,bubbles:true,cancelable:true});document.dispatchEvent(e);return e.defaultPrevented})"))
            compose.activityRule.scenario.onActivity{it.vm.navigate(b.id)};compose.onNodeWithTag("select-records").performClick();compose.onNodeWithTag("select-all-records").performClick();nativeRecordDrag("record-card-${n.id}","sidebar-book-${wrong.id}")
            compose.waitUntil(8000){compose.onAllNodesWithText("记事本类型不兼容，未移动任何记录。").fetchSemanticsNodes().isNotEmpty()};assertTrue(compose.activity.vm.state.value.workspace!!.notes.filter{it.id in setOf(n.id,n2.id)}.all{it.notebookId==b.id});screenshot("39-incompatible-record-drop")
            runBlocking{compose.activity.vm.flush()};compose.activityRule.scenario.onActivity{it.vm.openLocal()};ready(false);assertEquals(2,compose.activity.vm.state.value.workspace!!.notes.count{it.notebookId==b.id})
        }finally{shell("input touchscreen motionevent UP 1 1");shell("wm size reset");shell("wm density reset")}
    }

}
