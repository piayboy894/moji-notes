package app.moji.android.ui

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import app.moji.android.*
import app.moji.android.security.Envelope

@Composable fun AuthScreen(s: AppState, vm: MojiViewModel, activity: MainActivity, backup: Envelope?, import: () -> Unit) {
    var password by remember { mutableStateOf("") }; var confirmation by remember { mutableStateOf("") }
    val existing = s.exists || backup != null
    BoxWithConstraints(Modifier.fillMaxSize()) {
        val wide = maxWidth >= 900.dp && maxHeight >= 550.dp
        Row(Modifier.fillMaxSize()) {
            if (wide) Surface(Modifier.weight(1f).fillMaxHeight(), color = MaterialTheme.colorScheme.primaryContainer.copy(alpha = .5f)) {
                Column(Modifier.padding(56.dp), verticalArrangement = Arrangement.SpaceBetween) {
                    Brand()
                    Column { Text("YOUR PRIVATE CORNER", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.primary); Spacer(Modifier.height(24.dp)); Text("重要的，\n都放在这里。", fontSize = 48.sp, lineHeight = 70.sp, fontWeight = FontWeight.Medium); Spacer(Modifier.height(24.dp)); Text("文字的片刻，灵感的火花，\n还有只属于你的秘密。", color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    Text("安静记录，自在整理。", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
            Box(Modifier.weight(1f).fillMaxHeight(), contentAlignment = Alignment.Center) {
                Column(Modifier.widthIn(max = 440.dp).fillMaxWidth().verticalScroll(rememberScrollState()).padding(if (wide) 40.dp else 28.dp), verticalArrangement = Arrangement.spacedBy(18.dp)) {
                    if (!wide) { Brand(); Spacer(Modifier.height(28.dp)) }
                    Surface(shape = RoundedCornerShape(16.dp), color = MaterialTheme.colorScheme.primaryContainer) { Icon(Icons.Outlined.LockOpen, null, Modifier.padding(16.dp).size(28.dp), tint = MaterialTheme.colorScheme.primary) }
                    Text(if (existing) "欢迎回来" else "建立你的私人空间", style = MaterialTheme.typography.titleLarge)
                    Text(if (backup != null) "输入备份的主密码，恢复你的记录。" else if (existing) "输入主密码，继续你的记录。" else "一个主密码，妥帖保护所有记录。", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Field(if (existing) "主密码" else "设置主密码（至少 10 位）", password, Modifier.testTag("auth-password"), secret = true) { password = it }
                    if (!existing) Field("确认主密码", confirmation, Modifier.testTag("auth-confirm"), secret = true) { confirmation = it }
                    if (s.error.isNotBlank()) Text(s.error, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodyMedium)
                    Button(onClick = { vm.authenticate(password, confirmation, backup) }, enabled = !s.busy && !s.loading && password.isNotBlank() && (s.error != "本地数据无法读取，请从加密备份恢复" || backup != null), modifier = Modifier.fillMaxWidth().height(50.dp).testTag("create-vault"), shape = RoundedCornerShape(13.dp)) { if (s.busy || s.loading) CircularProgressIndicator(Modifier.size(21.dp), strokeWidth = 2.dp) else { Text(if (existing) "解锁空间" else "创建空间"); Spacer(Modifier.weight(1f)); Icon(Icons.Outlined.ArrowForward, null) } }
                    if (existing && s.biometricEnabled && backup == null) OutlinedButton(onClick = { vm.biometric.unlock(activity, vm::biometricUnlock, vm::message) }, modifier = Modifier.fillMaxWidth()) { Icon(Icons.Outlined.Fingerprint, null); Spacer(Modifier.width(8.dp)); Text("使用设备验证解锁") }
                    Text("AES-256-GCM 加密 · 主密码无法找回", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    HorizontalDivider(Modifier.padding(vertical = 6.dp), color = MaterialTheme.colorScheme.outlineVariant)
                    TextButton(onClick = vm::demo, modifier = Modifier.testTag("demo")) { Text("先逛逛演示空间"); Spacer(Modifier.width(8.dp)); Icon(Icons.Outlined.NorthEast, null, Modifier.size(16.dp)) }
                    TextButton(onClick = import) { Icon(Icons.Outlined.FileUpload, null, Modifier.size(17.dp)); Spacer(Modifier.width(8.dp)); Text("从加密备份恢复") }
                }
            }
        }
    }
}
@Composable fun Brand() { Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) { Image(painterResource(R.drawable.moji_icon), null, Modifier.size(39.dp)); Text("墨记", fontSize = 24.sp, fontWeight = FontWeight.SemiBold); Text("M O J I", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) } }
