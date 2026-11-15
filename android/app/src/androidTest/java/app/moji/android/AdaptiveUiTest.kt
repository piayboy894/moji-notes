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
    private fun ready() { compose.waitUntil(15000) { val s=compose.activity.vm.state.value; check(s.error.isBlank()){s.error}; s.workspace!=null } }
    private fun hideKeyboard() { compose.activityRule.scenario.onActivity { androidx.core.view.WindowCompat.getInsetsController(it.window,it.window.decorView).hide(androidx.core.view.WindowInsetsCompat.Type.ime()) }; Thread.sleep(350); compose.waitForIdle() }
    private fun screenshot(name: String) { compose.waitForIdle(); Thread.sleep(500); val path="/sdcard/Download/moji-native-ui/$name.png"; shell("mkdir -p /sdcard/Download/moji-native-ui"); shell("screencap -p $path"); check((shell("stat -c %s $path").trim().toLongOrNull()?:0)>1024) }
    @Test fun phoneTabletUltrawideAndRotationPreserveNativeEditing() {
        try {
            ready(); compose.activityRule.scenario.onActivity { it.vm.navigate("home") }; compose.onNodeWithTag("home-title").assertIsDisplayed(); screenshot("01-phone-home")
            compose.onNodeWithTag("book-Text").performClick()
            compose.activityRule.scenario.onActivity { it.vm.addNote() }
            compose.onNodeWithTag("note-title").performTextReplacement("Native layout test")
            compose.onNodeWithTag("note-content").performTextReplacement("# 原生编辑\n\nRotation and resize keep this text.")
            hideKeyboard(); val id=compose.activity.vm.state.value.selectedId!!
            compose.activityRule.scenario.onActivity { it.requestedOrientation=ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE }; Thread.sleep(1200)
            compose.onNodeWithTag("note-content").assertTextContains("Rotation and resize",substring=true); screenshot("02-phone-landscape")
            compose.activityRule.scenario.onActivity { it.requestedOrientation=ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED }
            resize(1600,2560,240); compose.onNodeWithTag("note-content").assertExists(); screenshot("03-tablet-portrait")
            resize(2560,1600,240); compose.onNodeWithTag("note-list").assertExists(); screenshot("04-tablet-landscape")
            resize(3440,1440,240); compose.onNodeWithTag("note-content").assertExists(); screenshot("05-wide-window")
            compose.onNode(hasText("工作台") and hasAnyAncestor(hasTestTag("wide-navigation"))).performClick(); screenshot("06-wide-home")
            compose.activityRule.scenario.onActivity { a -> val note=a.vm.state.value.workspace!!.notes.first{it.id==id}; a.vm.navigate(note.notebookId); a.vm.select(id) }
            resize(720,1280,320); compose.onNodeWithTag("note-content").assertTextContains("Rotation and resize",substring=true); screenshot("07-narrow-window")
            compose.activityRule.scenario.onActivity { it.onWindowFocusChanged(false) }; assertNotNull(compose.activity.vm.state.value.workspace)
        } catch(t:Throwable){runCatching{screenshot("failure-adaptive")};throw t} finally {shell("wm size reset");shell("wm density reset")}
    }
    @Test fun newRecordRequiresExplicitNotebookAndCloudSavesRemainGlobal() {
        ready(); compose.activityRule.scenario.onActivity { it.vm.navigate("home") }
        val before=compose.activity.vm.state.value.workspace!!.notes.size
        compose.onAllNodesWithContentDescription("新建记录").onFirst().performClick(); compose.onNodeWithText("选择记事本").assertIsDisplayed()
        assertEquals(before,compose.activity.vm.state.value.workspace!!.notes.size)
        val target=compose.activity.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Prompt}
        compose.onNodeWithTag("choose-book-${target.id}").performClick()
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
    @Test fun richToolsPersistStylesTasksTablesAndExplicitVariables() {
        try {
            ready();compose.activityRule.scenario.onActivity{a->a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Text}.id);a.vm.addNote()}
            compose.onNodeWithTag("note-content").performTextInput("Literal {{braces}}")
            compose.onNodeWithTag("note-content").performTextInputSelection(androidx.compose.ui.text.TextRange(0,7))
            compose.onNodeWithContentDescription("加粗").performClick()
            compose.waitUntil(5000){compose.activity.vm.selectedNote()!!.content.contains("**Literal**")}
            compose.onNodeWithTag("note-content").performTextInputSelection(androidx.compose.ui.text.TextRange(18))
            compose.onNodeWithContentDescription("待办事项").performScrollTo().performClick()
            compose.onNodeWithTag("task-text").performTextInput("Clickable task");compose.onNodeWithTag("task-check").performClick()
            compose.waitUntil(5000){compose.activity.vm.selectedNote()!!.content.contains("[x] Clickable task")}
            hideKeyboard();compose.onNodeWithContentDescription("插入表格").performScrollTo().performClick();compose.onNodeWithText("插入",substring=false).performClick()
            compose.onNodeWithTag("table-cell-0-0").performScrollTo().performTextInput("left|right")
            hideKeyboard();compose.onNodeWithText("添加列").performScrollTo().performClick()
            compose.waitUntil(5000){RichBlocks.parse(compose.activity.vm.selectedNote()!!.content).first{it.kind=="table"}.rows[0].size==3}
            val saved=compose.activity.vm.selectedNote()!!;assertEquals("left|right",RichBlocks.parse(saved.content).first{it.kind=="table"}.rows[0][0]);screenshot("10-rich-table")
            compose.onNodeWithContentDescription("撤销").performScrollTo().performClick()
            compose.waitUntil(5000){RichBlocks.parse(compose.activity.vm.selectedNote()!!.content).first{it.kind=="table"}.rows[0].size==2}
            compose.onNodeWithContentDescription("重做").performScrollTo().performClick()
            compose.waitUntil(5000){RichBlocks.parse(compose.activity.vm.selectedNote()!!.content).first{it.kind=="table"}.rows[0].size==3}
            compose.activityRule.scenario.onActivity{a->a.vm.navigate(a.vm.state.value.workspace!!.liveBooks.first{it.mode==NoteMode.Prompt}.id);a.vm.addNote()}
            compose.onNodeWithTag("note-content").performTextInput("Keep {{literal}} ");hideKeyboard()
            assertTrue(PromptVariables.used(compose.activity.vm.selectedNote()!!).isEmpty())
            compose.onNodeWithText("插入变量").performClick();compose.onNodeWithTag("variable-name").performTextInput("Audience");compose.onNodeWithText("插入",substring=false).performClick()
            compose.waitUntil(5000){PromptVariables.used(compose.activity.vm.selectedNote()!!).size==1}
            val prompt=compose.activity.vm.selectedNote()!!;assertTrue(prompt.content.contains("{{literal}}"));assertEquals("Audience",prompt.variables.single().name)
            compose.activityRule.scenario.onActivity{it.vm.navigate("home");it.vm.navigate(prompt.notebookId);it.vm.select(prompt.id)}
            compose.onNodeWithContentDescription("变量：Audience").assertExists();screenshot("11-explicit-variable")
        }catch(t:Throwable){runCatching{screenshot("failure-rich-tools")};throw t}
    }
    @Test fun shortcutsCanBeChangedWithoutActivatingCommandsDuringCapture() {
        ready(); compose.activityRule.scenario.onActivity{it.vm.navigate("home")}
        compose.onNodeWithContentDescription("打开导航").performClick(); compose.onNodeWithContentDescription("偏好设置").performClick()
        compose.onNodeWithText("快捷键").performClick(); compose.onNodeWithTag("shortcut-new").performScrollTo().performClick()
        compose.onNodeWithTag("shortcut-capture").performKeyInput { keyDown(Key.CtrlLeft); keyDown(Key.ShiftLeft); pressKey(Key.N); keyUp(Key.ShiftLeft); keyUp(Key.CtrlLeft) }
        compose.onNodeWithText("保存").performClick()
        assertEquals("Ctrl+Shift+N",compose.activity.vm.state.value.workspace!!.preferences.shortcuts["new"])
        compose.onNodeWithContentDescription("关闭").performClick()
        compose.onNodeWithTag("adaptive-layout").performKeyInput { keyDown(Key.CtrlLeft); keyDown(Key.ShiftLeft); pressKey(Key.N); keyUp(Key.ShiftLeft); keyUp(Key.CtrlLeft) }
        compose.onNodeWithText("选择记事本").assertIsDisplayed()
    }
}
