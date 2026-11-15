package app.moji.android.ui

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.*
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import app.moji.android.MojiViewModel
import app.moji.android.data.*

fun keyChord(event: KeyEvent): String {
    if (event.isMetaPressed) return ""
    val name = android.view.KeyEvent.keyCodeToString(event.nativeKeyEvent.keyCode).removePrefix("KEYCODE_")
    if (!Regex("[A-Z0-9]|F(?:[1-9]|1[0-2])").matches(name)) return ""
    return (if (event.isCtrlPressed) "Ctrl+" else "") + (if (event.isAltPressed) "Alt+" else "") + (if (event.isShiftPressed) "Shift+" else "") + name
}
@Composable fun ShortcutSettings(p: Preferences, vm: MojiViewModel) {
    var editing by remember { mutableStateOf<String?>(null) }
    SectionTitle("快捷键")
    Shortcuts.actions.forEach { (id, label, _) ->
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Text(label, Modifier.weight(1f), style = MaterialTheme.typography.bodyMedium)
            OutlinedButton(onClick = { editing = id }, modifier = Modifier.testTag("shortcut-$id")) { Text(Shortcuts.binding(p, id).ifBlank { "未设置" }) }
        }
    }
    TextButton(onClick = { vm.preferences { it.copy(shortcuts = emptyMap()) } }) { Text("恢复默认") }
    editing?.let { id ->
        var chord by remember(id) { mutableStateOf(Shortcuts.binding(p, id)) }
        val error = Shortcuts.error(p, id, chord); val focus = remember { FocusRequester() }
        PanelDialog(Shortcuts.actions.first { it.first == id }.second, { editing = null }, actions = {
            TextButton(onClick = { chord = "" }) { Text("清除") }
            Button(onClick = { vm.preferences { it.copy(shortcuts = it.shortcuts + (id to chord)) }; editing = null }, enabled = error == null) { Text("保存") }
        }) {
            OutlinedTextField(chord, {}, label = { Text("按下新的组合键") }, readOnly = true, singleLine = true,
                modifier = Modifier.fillMaxWidth().testTag("shortcut-capture").focusRequester(focus).onPreviewKeyEvent { event ->
                    if (event.type == KeyEventType.KeyDown) { val next = keyChord(event); if (next.isNotEmpty()) chord = next }; true
                })
            if (error != null) Text(error, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.labelSmall)
            LaunchedEffect(id) { focus.requestFocus() }
        }
    }
}
