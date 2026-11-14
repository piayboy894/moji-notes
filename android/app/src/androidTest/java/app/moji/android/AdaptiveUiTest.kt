package app.moji.android

import android.content.pm.ActivityInfo
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import app.moji.android.data.NoteMode
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class AdaptiveUiTest {
    @get:Rule val compose = createAndroidComposeRule<MainActivity>()
    private fun shell(command: String): String = InstrumentationRegistry.getInstrumentation().uiAutomation.executeShellCommand(command).use { descriptor -> android.os.ParcelFileDescriptor.AutoCloseInputStream(descriptor).use { it.readBytes().toString(Charsets.UTF_8) } }
    private fun resize(width: Int, height: Int, density: Int) { shell("wm size ${width}x$height"); shell("wm density $density"); Thread.sleep(1200); compose.waitForIdle() }
    private fun hideKeyboard() { compose.activityRule.scenario.onActivity { a -> androidx.core.view.WindowCompat.getInsetsController(a.window, a.window.decorView).hide(androidx.core.view.WindowInsetsCompat.Type.ime()) }; compose.waitForIdle() }
    private fun screenshot(name: String) {
        compose.waitForIdle()
        // UTP uninstalls the app after the suite, so keep CI captures outside its data directory.
        val path = "/sdcard/Download/moji-native-ui/$name.png"
        // UiAutomation executes individual commands, not shell control operators.
        shell("mkdir -p /sdcard/Download/moji-native-ui")
        shell("screencap -p $path")
        check((shell("stat -c %s $path").trim().toLongOrNull() ?: 0) > 1024) { "Native screenshot unavailable: $name" }
    }
    @Test fun phoneTabletUltrawideAndRotationPreserveNativeEditing() {
        try {
            compose.waitUntil(10000) { !compose.activity.vm.state.value.loading }; compose.onNodeWithTag("demo").performClick(); compose.onNodeWithTag("home-title").assertIsDisplayed()
            screenshot("01-phone-home")
            compose.onNodeWithTag("book-Text").performClick(); compose.onNodeWithText("把生活，写成喜欢的样子").performClick(); compose.onNodeWithTag("note-content").performTextReplacement("# 原生安卓编辑\n\nRotation and resize keep this text.\n\n- [x] Native Android")
            hideKeyboard(); compose.waitForIdle(); val id = compose.activity.vm.state.value.selectedId!!
            compose.activityRule.scenario.onActivity { it.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE }; Thread.sleep(1200)
            compose.waitUntil(10000) { compose.activity.vm.state.value.workspace!!.notes.first { it.id == id }.content.contains("Rotation and resize") }; compose.onNodeWithTag("note-content").assertTextContains("Rotation and resize", substring = true); screenshot("02-phone-landscape")
            compose.activityRule.scenario.onActivity { it.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED }
            resize(1600, 2560, 240); compose.onNodeWithTag("note-content").assertExists(); assertEquals(id, compose.activity.vm.state.value.selectedId); screenshot("03-tablet-portrait")
            resize(2560, 1600, 240); compose.onNodeWithTag("note-list").assertExists(); compose.onNodeWithTag("note-content").assertExists(); screenshot("04-tablet-landscape")
            resize(3440, 1440, 240); compose.onNodeWithTag("note-content").assertExists(); screenshot("05-dex-ultrawide-layout")
            resize(720, 1280, 320); compose.onNodeWithTag("note-content").assertExists(); assertTrue(compose.activity.vm.selectedNote()!!.content.contains("Rotation and resize")); screenshot("06-narrow-window")
            compose.activityRule.scenario.onActivity { it.onWindowFocusChanged(false) }; Thread.sleep(700); assertNotNull(compose.activity.vm.state.value.workspace)
            compose.activityRule.scenario.onActivity { it.onWindowFocusChanged(true); it.vm.clearSelection(); it.vm.navigate("home") }; compose.onNodeWithTag("home-title").assertExists()
        } finally { shell("wm size reset"); shell("wm density reset") }
    }
    @Test fun encryptedPersistenceAndPasswordModeWorkWithNativeFields() {
        compose.waitUntil(10000) { !compose.activity.vm.state.value.loading }
        if (compose.activity.vm.state.value.workspace != null) { compose.activityRule.scenario.onActivity { it.vm.lock() }; compose.waitUntil(10000) { compose.activity.vm.state.value.workspace == null } }
        compose.onNodeWithTag("auth-password").performTextInput("Android-Test-Password-2026")
        if (!compose.activity.vm.state.value.exists) compose.onNodeWithTag("auth-confirm").performTextInput("Android-Test-Password-2026")
        compose.onNodeWithTag("create-vault").performClick(); compose.waitUntil(15000) { compose.activity.vm.state.value.workspace != null }
        compose.onNodeWithTag("book-Password").performClick()
        compose.activityRule.scenario.onActivity { it.vm.addNote() }; compose.onNodeWithTag("account").performTextInput("native-test@example.com")
        compose.onNodeWithTag("credential-password").performTextInput("Private-Test-2026!"); hideKeyboard()
        runBlocking { compose.activity.vm.flush() }; val raw = compose.activity.vm.store.file.readText(); assertFalse(raw.contains("native-test@example.com")); assertFalse(raw.contains("Private-Test-2026!")); screenshot("07-native-password")
        compose.activityRule.scenario.onActivity { it.vm.lock() }; compose.waitUntil(10000) { compose.activity.vm.state.value.workspace == null }; assertNull(compose.activity.vm.sessionKey)
        compose.onNodeWithTag("auth-password").performTextInput("Android-Test-Password-2026"); compose.onNodeWithTag("create-vault").performClick(); compose.waitUntil(15000) { compose.activity.vm.state.value.workspace != null }; assertTrue(compose.activity.vm.state.value.workspace!!.notes.any { it.username == "native-test@example.com" })
    }
}
