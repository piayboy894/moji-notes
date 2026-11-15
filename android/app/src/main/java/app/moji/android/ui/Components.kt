package app.moji.android.ui

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.input.*
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.text.KeyboardOptions
import app.moji.android.data.NoteMode

fun modeIcon(mode: NoteMode): ImageVector = when (mode) { NoteMode.Text -> MojiIcons.Description; NoteMode.Prompt -> MojiIcons.AutoAwesome; NoteMode.Password -> MojiIcons.Key }
@Composable fun Tool(icon: ImageVector, label: String, enabled: Boolean = true, action: () -> Unit) { IconButton(onClick = action, enabled = enabled, modifier = Modifier.size(48.dp)) { Icon(icon, label, modifier = Modifier.size(24.dp)) } }
@Composable fun Field(label: String, value: String, modifier: Modifier = Modifier, lines: Int = 1, secret: Boolean = false, readOnly: Boolean = false, onChange: (String) -> Unit) {
    var visible by remember { mutableStateOf(false) }
    LaunchedEffect(visible) { if (visible && secret) { kotlinx.coroutines.delay(20000); visible = false } }
    OutlinedTextField(value, onChange, modifier.fillMaxWidth(), readOnly = readOnly, label = { Text(label) }, singleLine = lines == 1, minLines = lines, shape = RoundedCornerShape(12.dp), visualTransformation = if (secret && !visible) PasswordVisualTransformation() else VisualTransformation.None, keyboardOptions = KeyboardOptions(keyboardType = if (secret) KeyboardType.Password else KeyboardType.Text, autoCorrectEnabled = !secret), trailingIcon = if (secret) ({ Tool(if (visible) MojiIcons.VisibilityOff else MojiIcons.Visibility, if (visible) "隐藏" else "显示") { visible = !visible } }) else null)
}
@Composable fun PanelDialog(title: String, close: () -> Unit, width: Int = 580, scroll: Boolean = true, actions: @Composable RowScope.() -> Unit = {}, content: @Composable ColumnScope.() -> Unit) {
    Dialog(onDismissRequest = close, properties = DialogProperties(usePlatformDefaultWidth = false, decorFitsSystemWindows = false)) {
        BoxWithConstraints(Modifier.fillMaxSize().safeDrawingPadding().imePadding().padding(16.dp)) {
            Surface(Modifier.widthIn(max = width.dp).fillMaxWidth().heightIn(max = maxHeight).align(androidx.compose.ui.Alignment.Center), shape = RoundedCornerShape(24.dp), color = MaterialTheme.colorScheme.surface, tonalElevation = 2.dp) {
                Column(Modifier.padding(24.dp)) {
                    Row(Modifier.fillMaxWidth(), verticalAlignment = androidx.compose.ui.Alignment.CenterVertically) { Text(title, style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f)); Tool(MojiIcons.Close, "关闭", action = close) }
                    Spacer(Modifier.height(12.dp))
                    Column(Modifier.weight(1f, fill = false).then(if (scroll) Modifier.verticalScroll(rememberScrollState()) else Modifier), verticalArrangement = Arrangement.spacedBy(14.dp), content = content)
                    Row(Modifier.fillMaxWidth().padding(top = 20.dp), horizontalArrangement = Arrangement.End, verticalAlignment = androidx.compose.ui.Alignment.CenterVertically, content = actions)
                }
            }
        }
    }
}
@Composable fun Confirm(title: String, body: String, close: () -> Unit, action: () -> Unit) { AlertDialog(onDismissRequest = close, title = { Text(title) }, text = { Text(body) }, confirmButton = { TextButton(onClick = { action(); close() }) { Text("确认", color = MaterialTheme.colorScheme.error) } }, dismissButton = { TextButton(onClick = close) { Text("取消") } }) }
@Composable fun SectionTitle(title: String, subtitle: String? = null) { Column(verticalArrangement = Arrangement.spacedBy(10.dp)) { Text(title, style = MaterialTheme.typography.titleLarge); subtitle?.let { Text(it, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant) } } }
