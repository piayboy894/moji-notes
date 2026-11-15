package app.moji.android

import android.content.pm.ActivityInfo
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.input.key.Key
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import app.moji.android.data.*
import app.moji.android.sync.GameSaves
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@OptIn(androidx.compose.ui.test.ExperimentalTestApi::class)
@RunWith(AndroidJUnit4::class)
class AdaptiveUiTest {
    @get:Rule val compose = createAndroidComposeRule<MainActivity>()
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
    private fun focusWeb(){val web=webView();compose.activityRule.scenario.onActivity{web.requestFocus()};js("window.MojiEditor.editor.commands.focus()");compose.waitUntil(5000){js("Boolean(document.activeElement?.matches('.tiptap'))")=="true"}}
    private fun jsText():String=org.json.JSONTokener(js("window.MojiEditor.editor.getText() ")).nextValue().toString()
    private fun webReady(){compose.waitForIdle();val expected=org.json.JSONObject.quote(compose.activity.vm.state.value.selectedId);compose.waitUntil(30000){runCatching{js("Boolean(window.MojiEditor?.editor && window.MojiEditor.editor.isEditable)")=="true"&&js("window.MojiEditor.recordId")==expected}.getOrDefault(false)}}
    private fun tapWeb(selector:String){hideKeyboard();val native=webView();compose.activityRule.scenario.onActivity{native.scrollTo(0,0)};js("document.querySelector('.simple-editor-wrapper').scrollTop=0;window.scrollTo(0,0);document.querySelector('$selector').scrollIntoView({block:'start'})");Thread.sleep(250);val raw=js("JSON.stringify((()=>{const r=document.querySelector('$selector').getBoundingClientRect();const v=visualViewport;return {x:r.x+r.width/2-v.offsetLeft,y:r.y+r.height/2-v.offsetTop,width:v.width,height:r.height}})())");val point=org.json.JSONObject(org.json.JSONTokener(raw).nextValue().toString());val location=IntArray(2);var width=0;val web=webView();compose.activityRule.scenario.onActivity{web.getLocationOnScreen(location);width=web.width};val ratio=width/point.getDouble("width");println("Web tap $point native=${location.toList()} width=$width ratio=$ratio");shell("input tap ${(location[0]+point.getDouble("x")*ratio).toInt()} ${(location[1]+point.getDouble("y")*ratio).toInt()}")}

    private fun hideKeyboard() { compose.activityRule.scenario.onActivity { androidx.core.view.WindowCompat.getInsetsController(it.window,it.window.decorView).hide(androidx.core.view.WindowInsetsCompat.Type.ime()) }; Thread.sleep(350); compose.waitForIdle() }
    private fun screenshot(name: String) { compose.waitForIdle(); Thread.sleep(500); val path="/sdcard/Download/moji-native-ui/$name.png"; shell("mkdir -p /sdcard/Download/moji-native-ui"); shell("screencap -p $path"); check((shell("stat -c %s $path").trim().toLongOrNull()?:0)>1024) }
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
        hideKeyboard();compose.onNodeWithText("插入变量").performClick();compose.onNodeWithTag("variable-name").performTextInput("Audience");compose.onNodeWithText("插入",substring=false).performClick()
        compose.waitUntil(5000){PromptVariables.used(compose.activity.vm.selectedNote()!!).size==1};assertTrue(jsText().contains("{{literal}}"))
        js("window.MojiEditor.editor.chain().focus('end').setImage({src:'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZuoAAAAASUVORK5CYII='}).run()")
        compose.waitUntil(5000){compose.activity.vm.selectedNote()!!.content.contains("data:image/")};runBlocking{compose.activity.vm.flush()}
        val saved=compose.activity.vm.selectedNote()!!;screenshot("10-shared-editor")
        compose.activityRule.scenario.onActivity{it.vm.navigate("home")};compose.onNodeWithTag("home-title").assertExists();compose.activityRule.scenario.onActivity{it.vm.navigate(saved.notebookId);it.vm.select(saved.id)};webReady()
        assertTrue(jsText().contains("Audience"));assertTrue(js("Boolean(document.querySelector('.tiptap img')?.complete)")=="true");assertFalse(js("window.MojiEditor.editor.can().undo()")=="true");screenshot("11-explicit-variable")
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
        compose.onNodeWithTag("book-Prompt").performTouchInput{longClick()}
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
        webReady();tapWeb(".tiptap");InstrumentationRegistry.getInstrumentation().sendStringSync(marker)
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
        }catch(t:Throwable){runCatching{screenshot("failure-physical-keyboard")};throw t}
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
}
