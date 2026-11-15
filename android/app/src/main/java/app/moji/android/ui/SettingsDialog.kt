package app.moji.android.ui

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.*
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
    var repo by remember { mutableStateOf(w.sync.repository) }; var token by remember { mutableStateOf(w.sync.token) }; var code by remember { mutableStateOf(w.sync.code) }
    var old by remember { mutableStateOf("") }; var next by remember { mutableStateOf("") }; var again by remember { mutableStateOf("") }; var status by remember { mutableStateOf("") }; var changing by remember { mutableStateOf(false) }
    PanelDialog("偏好设置", close, width = 720) {
        Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) { listOf("general" to "通用", "sync" to "存档", "security" to "安全", "data" to "数据", "about" to "更新").forEach { (id, title) -> FilterChip(tab == id, { tab = id; status = "" }, label = { Text(title) }) } }
        when (tab) {
            "general" -> {
                SectionTitle("通用设置")
                Text("外观", style = MaterialTheme.typography.labelLarge)
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) { listOf("light" to "浅色", "dark" to "深色", "system" to "跟随系统").forEach { (id, title) -> FilterChip(w.preferences.theme == id, { vm.preferences { it.copy(theme = id) } }, label = { Text(title) }) } }
                Text("编辑器字号 · ${w.preferences.fontSize.toInt()}", style = MaterialTheme.typography.labelLarge)
                Slider(w.preferences.fontSize.toFloat(), { value -> vm.preferences { it.copy(fontSize = value.toInt().toDouble()) } }, valueRange = 11f..28f, steps = 16)
                SettingRow("显示行号") { Switch(w.preferences.lineNumbers, { checked -> vm.preferences { it.copy(lineNumbers = checked) } }) }
                HorizontalDivider(); Text("DeX / 键盘快捷键", style = MaterialTheme.typography.titleMedium)
                listOf("全局搜索" to "Ctrl K", "新建记录" to "Ctrl N", "查找与替换" to "Ctrl F / H", "保存历史版本" to "Ctrl S", "全局云存档" to "Ctrl Shift S", "锁定空间" to "Ctrl Shift L", "撤销 / 重做" to "Ctrl Z / Y").forEach { (label, key) -> Row(Modifier.fillMaxWidth()) { Text(label, Modifier.weight(1f), style = MaterialTheme.typography.bodyMedium); Text(key, color = MaterialTheme.colorScheme.onSurfaceVariant, style = MaterialTheme.typography.labelSmall) } }
            }
            "sync" -> {
                SectionTitle("云存档连接")
                Field("数据仓库", repo) { repo = it }; Field("GitHub Token", token, secret = true) { token = it.trim() }
                Text("仅需数据仓库 Contents 读写权限。Token 不会进入云端数据文件。", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Field("存档码（12–128 位字母或数字）", code, secret = true) { code = it.trim() }
                Row { TextButton(onClick = { code = Crypto.password(24, false) }) { Icon(Icons.Outlined.Casino, null, Modifier.size(17.dp)); Spacer(Modifier.width(6.dp)); Text("随机生成") }; TextButton(onClick = { activity.copy(code, true) }) { Text("复制存档码") } }
                fun config() = w.sync.copy(repository = repo.trim(), token = token, code = code, base = emptyMap())
                fun saveConnection() { vm.mutate { it.copy(sync = config(), preferences = it.preferences.copy(autoSync = false)) } }
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    OutlinedButton(onClick = { saveConnection(); status = "连接设置已保存" }, enabled = !s.demo) { Text("保存设置") }
                    Button(onClick = { saveConnection(); manageSaves() }, enabled = !s.demo && token.isNotBlank() && code.isNotBlank()) { Text("打开云存档") }
                }
                if (status.isNotBlank()) Text(status, style = MaterialTheme.typography.labelSmall)
                TextButton(onClick = { activity.openUrl("https://github.com/settings/personal-access-tokens/new") }) { Text("创建 GitHub 授权"); Icon(Icons.Outlined.OpenInNew, null, Modifier.size(15.dp)) }
            }
            "security" -> {
                SectionTitle("安全与隐私")
                Text("闲置自动锁定", style = MaterialTheme.typography.labelLarge)
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(7.dp)) { listOf(1, 5, 10, 15, 30, 60).forEach { minutes -> FilterChip(w.preferences.autoLockMinutes == minutes, { vm.preferences { it.copy(autoLockMinutes = minutes) } }, label = { Text("$minutes 分钟") }) } }
                Text("分屏失焦保持可见；进入后台 10 秒后锁定，锁屏立即锁定。", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                SettingRow("使用设备验证解锁") { Switch(s.biometricEnabled, { enabled -> if (enabled) { val key = vm.sessionKey; if (key == null || !vm.biometric.available()) vm.message("请先设置可用的设备生物识别或屏幕锁") else vm.biometric.enable(activity, key, { vm.biometricChanged(); vm.message("设备解锁已启用") }, vm::message) } else { vm.biometric.disable(); vm.biometricChanged() } }, enabled = !s.demo && !s.busy) }
                HorizontalDivider(); Text("更换主密码", style = MaterialTheme.typography.titleMedium)
                Field("当前主密码", old, secret = true) { old = it }; Field("新主密码（至少 10 位）", next, secret = true) { next = it }; Field("确认新主密码", again, secret = true) { again = it }
                if (status.isNotBlank()) Text(status, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.secondary)
                Button(onClick = { scope.launch { changing = true; try { require(next == again) { "两次输入的新主密码不一致" }; vm.changePassword(old, next); old = ""; next = ""; again = ""; status = "主密码已更换" } catch (error: Exception) { status = error.message ?: "更换失败" } finally { changing = false } } }, enabled = !s.demo && !changing && old.isNotBlank() && next.isNotBlank()) { Text(if (changing) "正在更换…" else "更换主密码") }
                OutlinedButton(onClick = { close(); vm.lock() }) { Icon(Icons.Outlined.Lock, null, Modifier.size(18.dp)); Spacer(Modifier.width(8.dp)); Text("立即锁定") }
            }
            "data" -> {
                SectionTitle("数据管理", "${w.liveBooks.size} 本记事本 · ${w.liveNotes.size} 条记录")
                DataOption("加密备份", "包含记录、历史版本与设置，用主密码恢复。", Icons.Outlined.Shield, !s.demo, backup)
                DataOption("Markdown 文本集", "按记事本导出 ZIP，不包含密码库。", Icons.Outlined.FolderZip, true, exportText)
                DataOption("导入与恢复", "支持 Markdown、TXT 与墨记加密备份。", Icons.Outlined.FileUpload, true, import)
                Text("本地与云端均加密保存。导出的 Markdown 是普通文本，请按需保管。GitHub 历史提交仍可能保留旧密文。", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                PasswordAudit(w, vm, close)
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
                Text("检测公开 Releases 无需 Token。下载后验证 SHA-256 和应用签名，由系统确认安装。", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                TextButton(onClick = { activity.openUrl("https://github.com/piayboy894/moji-notes/releases") }) { Icon(Icons.Outlined.OpenInNew, null, Modifier.size(17.dp)); Spacer(Modifier.width(7.dp)); Text("GitHub 项目与下载") }
            }
        }
    }
}
@Composable private fun SettingRow(title: String, trailing: @Composable () -> Unit) { Row(Modifier.fillMaxWidth().padding(vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) { Text(title, Modifier.weight(1f)); trailing() } }
@Composable private fun DataOption(title: String, hint: String, icon: androidx.compose.ui.graphics.vector.ImageVector, enabled: Boolean, action: () -> Unit) { OutlinedCard(onClick = action, enabled = enabled, modifier = Modifier.fillMaxWidth()) { Row(Modifier.padding(18.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(13.dp)) { Icon(icon, null, tint = MaterialTheme.colorScheme.primary); Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(7.dp)) { Text(title, style = MaterialTheme.typography.titleMedium); Text(hint, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }; Icon(Icons.Outlined.ChevronRight, null) } } }
@Composable private fun PasswordAudit(w: Workspace, vm: MojiViewModel, close: () -> Unit) { val notes = w.liveNotes.filter { n -> w.notebooks.first { it.id == n.notebookId }.mode == NoteMode.Password && n.password.isNotBlank() }; val weak = notes.filter { Records.strength(it.password) < 2 }; val duplicates = notes.filter { n -> notes.any { it.id != n.id && it.password == n.password } }; HorizontalDivider(); Text("密码健康", style = MaterialTheme.typography.titleMedium); Text("${weak.size} 个需要加强 · ${duplicates.size} 个使用重复密码", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant); (weak + duplicates).distinctBy { it.id }.forEach { note -> TextButton(onClick = { vm.navigate(note.notebookId); vm.select(note.id); close() }) { Icon(Icons.Outlined.Key, null, Modifier.size(18.dp)); Spacer(Modifier.width(10.dp)); Text(note.title) } } }
