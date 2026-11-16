package app.moji.android.ui

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import app.moji.android.*
import app.moji.android.data.*
import app.moji.android.security.Crypto
import kotlinx.coroutines.launch

@Composable fun SettingsDialog(s: AppState, vm: MojiViewModel, activity: MainActivity, initial: String, close: () -> Unit, backup: () -> Unit, exportText: () -> Unit, import: () -> Unit, manageSaves: () -> Unit) {
    val w = s.workspace ?: return; var tab by remember { mutableStateOf(initial) }; val scope = rememberCoroutineScope()
    var repo by remember { mutableStateOf(w.sync.repository) }; var token by remember { mutableStateOf(w.sync.token) }
    var status by remember { mutableStateOf("") }
    PanelDialog("偏好设置", close, width = 720) {
        Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) { listOf("general" to "通用", "sync" to "存档", "shortcuts" to "快捷键", "data" to "数据", "about" to "更新").forEach { (id, title) -> FilterChip(tab == id, { tab = id; status = "" }, label = { Text(title) }) } }
        when (tab) {
            "general" -> {
                SectionTitle("通用设置")
                ThemePicker(w.preferences,vm)
                Text("编辑器字号 · ${w.preferences.fontSize.toInt()}", style = MaterialTheme.typography.labelLarge)
                Slider(w.preferences.fontSize.toFloat(), { value -> vm.preferences { it.copy(fontSize = value.toInt().toDouble()) } }, valueRange = 11f..28f, steps = 16)

            }
            "sync" -> {
                SectionTitle("云存档连接")
                Field("数据仓库", repo) { repo = it }; Field("GitHub Token", token, secret = true) { token = it.trim() }
                Text("仅需数据仓库 Contents 读写权限。Token 不会进入云端数据文件。", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                fun config() = w.sync.copy(repository = repo.trim(), token = token, base = emptyMap())
                fun saveConnection() { vm.mutate { it.copy(sync = config(), preferences = it.preferences.copy(autoSync = false)) } }
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    OutlinedButton(onClick = { saveConnection(); status = "连接设置已保存" }) { Text("保存设置") }
                    Button(onClick = { saveConnection(); manageSaves() }, enabled = token.isNotBlank() && repo.isNotBlank()) { Text("打开云存档") }
                }
                if (status.isNotBlank()) Text(status, style = MaterialTheme.typography.labelSmall)
                TextButton(onClick = { activity.openUrl("https://github.com/settings/personal-access-tokens/new") }) { Text("创建 GitHub 授权"); Icon(MojiIcons.OpenInNew, null, Modifier.size(15.dp)) }
            }
            "shortcuts" -> ShortcutSettings(w.preferences, vm)
            "data" -> {
                SectionTitle("数据管理", "${w.liveBooks.size} 本记事本 · ${w.liveNotes.size} 条记录")
                DataOption("本地存档", "全部记事本与记录。", MojiIcons.Shield, true, backup)
                DataOption("文本集", "按记事本导出 ZIP，不包含密码库。", MojiIcons.FolderZip, true, exportText)
                DataOption("导入与恢复", "支持 Markdown、TXT 与墨记存档。", MojiIcons.FileUpload, true, import)
            }
            else -> {
                SectionTitle("墨记  M O J I", "Android ${BuildConfig.VERSION_NAME}")
                Text(s.update?.let { "发现新版本 ${it.version}" } ?: "软件更新", style = MaterialTheme.typography.titleMedium)
                s.updateProgress?.let { progress -> LinearProgressIndicator(progress = { progress }, modifier = Modifier.fillMaxWidth()); Text("正在下载 ${(progress * 100).toInt()}%", style = MaterialTheme.typography.labelSmall) }
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    OutlinedButton(onClick = { vm.checkUpdate() }, enabled = !s.updateChecking) { Text(if (s.updateChecking) "正在检查…" else "检查更新") }
                    if (s.downloadedApk != null) Button(onClick = activity::installUpdate) { Text("安装更新") }
                    else if (s.update != null) Button(onClick = { vm.downloadUpdate() }, enabled = s.updateProgress == null) { Text("下载更新") }
                }
                TextButton(onClick = { activity.openUrl("https://github.com/piayboy894/moji-notes/releases") }) { Icon(MojiIcons.OpenInNew, null, Modifier.size(17.dp)); Spacer(Modifier.width(7.dp)); Text("GitHub 项目与下载") }
            }
        }
    }
}
@Composable private fun SettingRow(title: String, trailing: @Composable () -> Unit) { Row(Modifier.fillMaxWidth().padding(vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) { Text(title, Modifier.weight(1f)); trailing() } }
@Composable private fun DataOption(title: String, hint: String, icon: androidx.compose.ui.graphics.vector.ImageVector, enabled: Boolean, action: () -> Unit) { OutlinedCard(onClick = action, enabled = enabled, modifier = Modifier.fillMaxWidth()) { Row(Modifier.padding(18.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(13.dp)) { Icon(icon, null, tint = MaterialTheme.colorScheme.primary); Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(7.dp)) { Text(title, style = MaterialTheme.typography.titleMedium); Text(hint, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }; Icon(MojiIcons.ChevronRight, null) } } }
