package app.moji.android

import android.content.*
import android.content.res.Configuration
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.view.DragEvent
import android.view.WindowManager
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.fragment.app.FragmentActivity
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import androidx.window.layout.FoldingFeature
import androidx.window.layout.WindowInfoTracker
import app.moji.android.data.Records
import app.moji.android.ui.MojiRoot
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.launch

data class WindowPosture(val hingeLeft: Int? = null, val hingeTop: Int? = null, val hingeWidth: Int = 0, val hingeHeight: Int = 0, val separating: Boolean = false)
class MainActivity : FragmentActivity() {
    val vm: MojiViewModel by viewModels { viewModelFactory { initializer { MojiViewModel(application) } } }
    val posture = MutableStateFlow(WindowPosture())
    val editorSession by lazy { app.moji.android.ui.SharedEditorSession(this,vm) }
    private var copiedSecret: String? = null
    private val screenOff = object : BroadcastReceiver() { override fun onReceive(context: Context?, intent: Intent?) { if (intent?.action == Intent.ACTION_SCREEN_OFF) { clearSecretClipboard(); vm.background() } } }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState); app.moji.android.data.ThemeCatalog.initialize(this); enableEdgeToEdge()
        if (Build.VERSION.SDK_INT >= 33) registerReceiver(screenOff, IntentFilter(Intent.ACTION_SCREEN_OFF), Context.RECEIVER_NOT_EXPORTED) else @Suppress("DEPRECATION") registerReceiver(screenOff, IntentFilter(Intent.ACTION_SCREEN_OFF))
        lifecycleScope.launch { WindowInfoTracker.getOrCreate(this@MainActivity).windowLayoutInfo(this@MainActivity).collect { info -> val fold = info.displayFeatures.filterIsInstance<FoldingFeature>().firstOrNull(); posture.value = fold?.let { WindowPosture(if (it.orientation == FoldingFeature.Orientation.VERTICAL) it.bounds.left else null, if (it.orientation == FoldingFeature.Orientation.HORIZONTAL) it.bounds.top else null, it.bounds.width(), it.bounds.height(), it.isSeparating) } ?: WindowPosture() } }
        receive(intent); setContent { MojiRoot(vm, this) }
        editorSession.preload()
        lifecycleScope.launch { vm.state.collect { s -> if (s.workspace == null) clearSecretClipboard() } }
    }
    override fun onNewIntent(intent: Intent) { super.onNewIntent(intent); setIntent(intent); receive(intent) }
    private fun receive(intent: Intent?) { if (intent?.action == Intent.ACTION_SEND && intent.type == "text/plain") intent.getStringExtra(Intent.EXTRA_TEXT)?.takeIf { it.length <= 2_000_000 }?.let { vm.importText(intent.getStringExtra(Intent.EXTRA_SUBJECT) ?: "接收的文字", it,notebookId="choose") } }
    override fun onStart() { super.onStart(); vm.foreground() }
    override fun onStop() { if (!isChangingConfigurations) vm.background(); super.onStop() }
    override fun onConfigurationChanged(newConfig: Configuration) { super.onConfigurationChanged(newConfig); vm.touch() }
    override fun onDestroy() { runCatching { unregisterReceiver(screenOff) }; editorSession.dispose(); super.onDestroy() }
    fun copy(value: String, secret: Boolean = false) {
        if (value.isEmpty()) { vm.message("没有可复制的内容"); return }
        val clipboard = getSystemService(ClipboardManager::class.java); val clip = ClipData.newPlainText("墨记", value)
        if (secret && Build.VERSION.SDK_INT >= 33) clip.description.extras = android.os.PersistableBundle().apply { putBoolean(ClipDescription.EXTRA_IS_SENSITIVE, true) }
        clipboard.setPrimaryClip(clip)
        if (secret) { copiedSecret = value; lifecycleScope.launch { delay(30000); if (copiedSecret == value) clearSecretClipboard() } }
        vm.message(if (secret) "已复制，30 秒后清除剪贴板" else "已复制")
    }
    @Suppress("DEPRECATION") private fun clearSecretClipboard() {
        val value = copiedSecret ?: return; copiedSecret = null
        val clipboard = getSystemService(ClipboardManager::class.java)
        runCatching { if (clipboard.primaryClip?.getItemAt(0)?.text?.toString() == value) { if (Build.VERSION.SDK_INT >= 28) clipboard.clearPrimaryClip() else clipboard.setPrimaryClip(ClipData.newPlainText("", "")) } }
    }
    fun secure(enabled: Boolean) { val wanted = enabled && !BuildConfig.DEBUG; val current = window.attributes.flags and WindowManager.LayoutParams.FLAG_SECURE != 0; if (wanted != current) { if (wanted) window.addFlags(WindowManager.LayoutParams.FLAG_SECURE) else window.clearFlags(WindowManager.LayoutParams.FLAG_SECURE) } }
    fun share(title: String, text: String) { startActivity(Intent.createChooser(Intent(Intent.ACTION_SEND).apply { type = "text/plain"; putExtra(Intent.EXTRA_SUBJECT, title); putExtra(Intent.EXTRA_TEXT, text) }, "分享记录")) }
    fun openUrl(value: String) { val uri = Uri.parse(value); if (uri.scheme in listOf("http", "https")) runCatching { startActivity(Intent(Intent.ACTION_VIEW, uri)) }.onFailure { vm.message("没有可打开链接的应用") } else vm.message("请填写 http 或 https 地址") }
    fun installUpdate() { val file = vm.state.value.downloadedApk ?: return; if (Build.VERSION.SDK_INT >= 26 && !packageManager.canRequestPackageInstalls()) { startActivity(Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:$packageName"))); vm.message("授权后返回墨记，再点击安装更新"); return }; runCatching { startActivity(vm.updater.installIntent(file)) }.onFailure { vm.message("无法打开系统安装程序") } }
}
