package app.moji.android.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import app.moji.android.AppState
import app.moji.android.MojiViewModel
import app.moji.android.sync.*
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@Composable fun CloudSavesDialog(vm: MojiViewModel, s: AppState, close: () -> Unit, settings: () -> Unit) {
    val workspace = s.workspace ?: return
    var catalog by remember { mutableStateOf<SaveCatalog?>(null) }
    var selectedId by remember { mutableStateOf<String?>(null) }
    var busy by remember { mutableStateOf(false) }; var status by remember { mutableStateOf("") }; var failed by remember { mutableStateOf(false) }
    var naming by remember { mutableStateOf(false) }; var overwrite by remember { mutableStateOf(false) }; var name by remember { mutableStateOf("") }
    var confirmation by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope(); val selected = catalog?.entries?.firstOrNull { it.id == selectedId }
    fun operate(action: suspend () -> Unit) { scope.launch { busy = true; failed = false; status = ""; try { action() } catch (cancel: CancellationException) { throw cancel } catch (error: Exception) { failed = true; status = error.message ?: "操作失败" } finally { busy = false } } }
    PanelDialog("云存档", { if (!busy) close() }, width = 820) {
        Row(Modifier.fillMaxWidth().testTag("global-saves"), verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) { Text("全部记事本", style = MaterialTheme.typography.titleMedium); Text("${workspace.liveBooks.size} 本 · ${workspace.liveNotes.size} 条记录", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            Tool(MojiIcons.Settings, "存档连接设置", !busy, settings)
            Tool(MojiIcons.Refresh, "刷新存档列表", !busy) { operate { catalog = vm.listCloudSaves(); selectedId = null; status = "已刷新" } }
        }
        BoxWithConstraints {
            val ready = catalog != null && !busy
            val actions: @Composable RowScope.(Boolean) -> Unit = { first ->
                if (first) {
                    OutlinedButton(onClick = { overwrite = false; name = "存档 " + SimpleDateFormat("MM-dd HH:mm", Locale.getDefault()).format(Date()); naming = true }, enabled = ready, modifier = Modifier.weight(1f).testTag("save-create")) { Text("新建存档") }
                    OutlinedButton(onClick = { overwrite = true; name = selected!!.name; naming = true }, enabled = ready && selected != null, modifier = Modifier.weight(1f).testTag("save-overwrite")) { Text("覆盖存档") }
                } else {
                    Button(onClick = { confirmation = "read" }, enabled = ready && selected != null, modifier = Modifier.weight(1f).testTag("save-read")) { Text("读取存档") }
                    OutlinedButton(onClick = { confirmation = "delete" }, enabled = ready && selected != null, modifier = Modifier.weight(1f).testTag("save-delete")) { Text("删除存档") }
                }
            }
            if (maxWidth < 580.dp) Column(verticalArrangement = Arrangement.spacedBy(8.dp)) { Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { actions(true) }; Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { actions(false) } }
            else Row(horizontalArrangement = Arrangement.spacedBy(9.dp)) { actions(true); actions(false) }
        }
        if (busy) LinearProgressIndicator(Modifier.fillMaxWidth())
        if (status.isNotBlank()) Text(status, style = MaterialTheme.typography.labelSmall, color = if (failed) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant)
        if (catalog == null) Box(Modifier.fillMaxWidth().padding(vertical = 35.dp), contentAlignment = Alignment.Center) { Text("点击刷新读取云存档", color = MaterialTheme.colorScheme.onSurfaceVariant) }
        else if (catalog!!.entries.isEmpty()) Box(Modifier.fillMaxWidth().padding(vertical = 35.dp), contentAlignment = Alignment.Center) { Text("还没有云存档", color = MaterialTheme.colorScheme.onSurfaceVariant) }
        else LazyColumn(Modifier.fillMaxWidth().heightIn(max = 360.dp).testTag("save-list"), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            items(catalog!!.entries, key = { it.id }) { entry ->
                Surface(onClick = { if (!busy) selectedId = entry.id }, shape = RoundedCornerShape(13.dp), color = if (selectedId == entry.id) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surface, border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant), modifier = Modifier.fillMaxWidth().testTag("cloud-save-${entry.id}")) {
                    Row(Modifier.padding(17.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                        Icon(MojiIcons.Save, null, tint = MaterialTheme.colorScheme.primary)
                        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(7.dp)) {
                            Text(entry.name, maxLines = 1, overflow = TextOverflow.Ellipsis)
                            Text("${entry.bookCount} 本 · ${entry.noteCount} 条 · ${entry.platform}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            Text(if (entry.savedAt > 0) SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.getDefault()).format(Date(entry.savedAt)) else "旧版单文件存档", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        RadioButton(selectedId == entry.id, onClick = null)
                    }
                }
            }
        }
    }
    if (naming) PanelDialog(if (overwrite) "覆盖存档" else "新建存档", { naming = false }, width = 460, actions = {
        Button(onClick = { val target = if (overwrite) selected else null; naming = false; operate { catalog = vm.writeCloudSave(catalog!!, name, target); if (target == null) selectedId = null; status = if (target == null) "存档已新建" else "存档已覆盖" } }, enabled = name.isNotBlank()) { Text(if (overwrite) "确认覆盖" else "创建") }
    }) { OutlinedTextField(name, { name = it.take(60) }, Modifier.fillMaxWidth().testTag("save-name"), singleLine = true, enabled = !(overwrite && selected?.legacy == true), label = { Text("存档名称") }); Text(if (overwrite) "用本机全部记事本和记录替换这份云存档。" else "保存本机全部记事本和记录。", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant) }
    if (confirmation != null && selected != null) Confirm(if (confirmation == "read") "读取「${selected.name}」？" else "删除「${selected.name}」？", if (confirmation == "read") "将替换本机全部记事本和记录，不合并。" else "删除这份云存档，本机内容保持不变。", { confirmation = null }) {
        val target = selected; val operation = confirmation
        operate { if (operation == "read") { vm.readCloudSave(catalog!!, target); status = "存档已读取" } else { catalog = vm.deleteCloudSave(catalog!!, target); selectedId = null; status = "存档已删除" } }
    }
}
