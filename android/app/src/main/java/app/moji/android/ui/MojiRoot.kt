package app.moji.android.ui

import android.app.Activity
import android.content.Intent
import android.net.Uri
import android.provider.OpenableColumns
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.*
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
import androidx.compose.ui.input.key.*
import androidx.compose.ui.input.pointer.*
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.layout.positionInWindow
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.unit.dp
import androidx.core.view.WindowCompat
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.moji.android.*
import app.moji.android.data.*
import app.moji.android.security.Envelope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.ByteArrayOutputStream
import java.io.InputStream
import java.io.OutputStream
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream

@OptIn(ExperimentalMaterial3Api::class)
@Composable fun MojiRoot(vm: MojiViewModel, activity: MainActivity) {
    val s by vm.state.collectAsStateWithLifecycle(); val posture by activity.posture.collectAsStateWithLifecycle()
    var settings by remember { mutableStateOf<String?>(null) }; var bookDialog by remember { mutableStateOf<Notebook?>(null) }; var newBook by remember { mutableStateOf(false) }; var templates by remember { mutableStateOf(false) }; var globalSearch by remember { mutableStateOf(false) }
    var backup by remember { mutableStateOf<Envelope?>(null) }; var backupPassword by remember { mutableStateOf("") }; var backupError by remember { mutableStateOf("") }; var restoring by remember { mutableStateOf(false) }
    var exportWriter by remember { mutableStateOf<(suspend (OutputStream) -> Unit)?>(null) }
    val scope = rememberCoroutineScope(); val snackbar = remember { SnackbarHostState() }; val drawer = rememberDrawerState(DrawerValue.Closed)
    fun name(uri: Uri): String = runCatching { activity.contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { if (it.moveToFirst()) it.getString(0) else null } }.getOrNull() ?: "导入记录"
    val importFile = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri -> if (uri != null) scope.launch {
        try { val fileName = name(uri); val text = withContext(Dispatchers.IO) { activity.contentResolver.openInputStream(uri)?.use { readLimited(it, 30_000_000).toString(Charsets.UTF_8) } ?: error("无法读取文件") }
            if (fileName.endsWith(".moji", true) || fileName.endsWith(".mojibak", true) || fileName.endsWith(".json", true)) { backup = VaultStore.decodeEnvelope(text); backupPassword = ""; backupError = "" }
            else if (s.workspace == null) vm.message("请先创建或解锁空间，再导入普通文本") else vm.importText(fileName.substringBeforeLast('.', fileName), text)
        } catch (error: Exception) { vm.message(error.message ?: "导入失败") }
    } }
    val createFile = rememberLauncherForActivityResult(ActivityResultContracts.StartActivityForResult()) { result -> val writer = exportWriter; exportWriter = null; val uri = result.data?.data
        if (result.resultCode == Activity.RESULT_OK && uri != null && writer != null) scope.launch { try { withContext(Dispatchers.IO) { activity.contentResolver.openOutputStream(uri)?.use { writer(it) } ?: error("无法写入目标文件") }; vm.message("导出完成") } catch (error: Exception) { vm.message(error.message ?: "导出失败") } }
    }
    fun createDocument(fileName: String, mime: String, writer: suspend (OutputStream) -> Unit) { exportWriter = writer; createFile.launch(Intent(Intent.ACTION_CREATE_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE).setType(mime).putExtra(Intent.EXTRA_TITLE, fileName)) }
    fun exportNote(note: Note) { createDocument(Records.safeName(note.title) + ".md", "text/markdown") { it.write(note.content.toByteArray(Charsets.UTF_8)) } }
    fun exportBackup() { scope.launch { try { val content = vm.encryptedBackup(); createDocument("墨记-加密备份-${java.time.LocalDate.now()}.moji", "application/octet-stream") { it.write(content.toByteArray(Charsets.UTF_8)) } } catch (error: Exception) { vm.message(error.message ?: "备份失败") } } }
    fun exportTexts() { val w = s.workspace ?: return; createDocument("墨记-文本集-${java.time.LocalDate.now()}.zip", "application/zip") { stream -> ZipOutputStream(stream).use { zip -> w.liveNotes.forEach { note -> val b = w.notebooks.first { it.id == note.notebookId }; if (b.mode != NoteMode.Password) { zip.putNextEntry(ZipEntry("${Records.safeName(b.name)}/${Records.safeName(note.title)}-${note.id.take(6)}.md")); zip.write("# ${note.title}\n\n${note.content}".toByteArray(Charsets.UTF_8)); zip.closeEntry() } } } } }
    val importImage = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri -> if (uri != null) scope.launch { try {
        val mime = activity.contentResolver.getType(uri) ?: "image/png"; require(mime in listOf("image/png", "image/jpeg", "image/gif", "image/webp")) { "支持 PNG、JPG、GIF、WebP 图片" }
        val bytes = withContext(Dispatchers.IO) { activity.contentResolver.openInputStream(uri)?.use { readLimited(it, 2_000_000) } ?: error("图片读取失败") }
        val note = vm.selectedNote() ?: return@launch; val cursor = vm.cursors[note.id]?.first?.coerceIn(0, note.content.length) ?: note.content.length
        vm.content(note.id, note.content.substring(0, cursor) + "\n![图片](data:$mime;base64,${app.moji.android.security.Crypto.b64(bytes)})\n" + note.content.substring(cursor))
    } catch (error: Exception) { vm.message(error.message ?: "图片导入失败") } } }
    LaunchedEffect(Unit) { vm.messages.collect { snackbar.showSnackbar(it) } }
    LaunchedEffect(s.workspace == null) { if (s.workspace == null) { settings = null; bookDialog = null; newBook = false; templates = false; globalSearch = false; backup = null; exportWriter = null } else { backup = null } }
    val dark = s.workspace?.preferences?.theme == "dark" || ((s.workspace?.preferences?.theme ?: "system") == "system" && isSystemInDarkTheme())
    SideEffect { WindowCompat.getInsetsController(activity.window, activity.window.decorView).apply { isAppearanceLightStatusBars = !dark; isAppearanceLightNavigationBars = !dark }; val selected = s.workspace?.notes?.firstOrNull { it.id == s.selectedId }; activity.secure((selected != null && s.workspace?.notebooks?.firstOrNull { it.id == selected.notebookId }?.mode == NoteMode.Password) || settings != null || backup != null) }
    MojiTheme(s.workspace?.preferences?.theme ?: "system") {
        Surface(Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
            BoxWithConstraints(Modifier.fillMaxSize().safeDrawingPadding().imePadding().onPreviewKeyEvent { event ->
                if (event.type == KeyEventType.KeyDown) vm.touch()
                if (s.workspace == null || event.type != KeyEventType.KeyDown || !event.isCtrlPressed) false else when (event.key) { Key.K -> { globalSearch = true; true }; Key.N -> { vm.addNote(); true }; Key.S -> { vm.selectedNote()?.let { vm.checkpoint(it.id) }; true }; Key.L -> if (event.isShiftPressed) { vm.lock(); true } else false; else -> false }
            }.pointerInput(Unit) { awaitPointerEventScope { while (true) { val event = awaitPointerEvent(PointerEventPass.Initial); if (event.changes.any { it.pressed }) vm.touch() } } }) {
                if (s.workspace == null) { AuthScreen(s, vm, activity, backup, { importFile.launch(arrayOf("application/octet-stream", "application/json", "*/*")) }); Box(Modifier.fillMaxSize(), contentAlignment = Alignment.BottomCenter) { SnackbarHost(snackbar) }; return@BoxWithConstraints }
                val wide = maxWidth >= 1100.dp && maxHeight >= 480.dp; val rail = !wide && maxWidth >= 720.dp && maxHeight >= 480.dp
                val navigationWidth = if (wide) 224.dp else if (rail) 72.dp else 0.dp
                val twoPane = maxWidth - navigationWidth >= 680.dp && maxHeight >= 480.dp
                val active = s.workspace?.notes?.firstOrNull { it.id == s.selectedId && it.purgedAt == null }
                BackHandler(enabled = active != null || s.view != "home") { if (active != null) vm.clearSelection() else vm.navigate("home") }
                ModalNavigationDrawer(drawerState = drawer, gesturesEnabled = !wide && !rail, drawerContent = { ModalDrawerSheet(Modifier.width(280.dp), drawerContainerColor = MaterialTheme.colorScheme.surface) { SideNavigation(s, vm, { scope.launch { drawer.close() } }, { settings = "general" }, { newBook = true }, { globalSearch = true }, { settings = "sync" }) } }) {
                    Row(Modifier.fillMaxSize().semantics { stateDescription = if (wide && twoPane && s.view != "home") "三栏布局" else if (twoPane && s.view != "home") "双栏布局" else "单栏布局" }.testTag("adaptive-layout")) {
                        if (wide) { Surface(Modifier.width(224.dp).fillMaxHeight(), color = MaterialTheme.colorScheme.surface) { SideNavigation(s, vm, {}, { settings = "general" }, { newBook = true }, { globalSearch = true }, { settings = "sync" }) }; VerticalDivider(color = MaterialTheme.colorScheme.outlineVariant) }
                        else if (rail) { CompactNavigation(s, vm, { settings = "general" }, { scope.launch { drawer.open() } }); VerticalDivider(color = MaterialTheme.colorScheme.outlineVariant) }
                        Scaffold(Modifier.weight(1f).fillMaxHeight(), containerColor = MaterialTheme.colorScheme.background,
                            topBar = { if (twoPane || active == null || s.view == "home") TopAppBar(title = { Text(viewTitle(s), style = MaterialTheme.typography.titleMedium, maxLines = 1) }, navigationIcon = { if (!wide && !rail) Tool(Icons.Outlined.Menu, "打开导航") { scope.launch { drawer.open() } } }, actions = { if (s.saving) CircularProgressIndicator(Modifier.size(15.dp), strokeWidth = 2.dp) else if (s.saveError) Icon(Icons.Outlined.ErrorOutline, "保存失败", tint = MaterialTheme.colorScheme.error) else if (s.demo) Text("演示", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.secondary); Tool(Icons.Outlined.Search, "全局搜索") { globalSearch = true }; Tool(Icons.Outlined.Add, "新建记录") { if (s.view == "home") templates = true else vm.addNote() } }, colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background)) },
                            snackbarHost = { SnackbarHost(snackbar) },
                            floatingActionButton = { if (!twoPane && active == null && s.view != "trash") FloatingActionButton(onClick = { if (s.view == "home") templates = true else vm.addNote() }, containerColor = MaterialTheme.colorScheme.primary) { Icon(Icons.Outlined.Add, "新建记录", tint = MaterialTheme.colorScheme.onPrimary) } }
                        ) { padding ->
                            Box(Modifier.padding(padding).fillMaxSize()) {
                                if (s.view == "home") HomeScreen(vm, s, { newBook = true }, { bookDialog = it }, { templates = true })
                                else if (twoPane) {
                                    var contentX by remember { mutableFloatStateOf(0f) }; val density = LocalDensity.current
                                    BoxWithConstraints(Modifier.fillMaxSize().onGloballyPositioned { contentX = it.positionInWindow().x }) {
                                        val seam = posture.hingeLeft?.let { with(density) { (it - contentX).toDp() } }
                                        val listWidth = if (posture.separating && seam != null && seam > 180.dp && seam < maxWidth - 260.dp) seam else if (maxWidth > 1200.dp) 320.dp else 276.dp
                                        val gap = if (posture.separating && seam == listWidth) with(density) { posture.hingeWidth.toDp().coerceAtLeast(8.dp) } else 1.dp
                                        Row(Modifier.fillMaxSize()) { NoteList(vm, s, Modifier.width(listWidth), { bookDialog = it }, { templates = true }); Spacer(Modifier.width(gap).fillMaxHeight().background(MaterialTheme.colorScheme.outlineVariant)); if (active != null) EditorScreen(vm, s, active, activity, ::exportNote, { importImage.launch("image/*") }, Modifier.weight(1f)) else EmptyEditor(Modifier.weight(1f)) }
                                    }
                                } else if (active != null) EditorScreen(vm, s, active, activity, ::exportNote, { importImage.launch("image/*") }, showBack = true, back = vm::clearSelection)
                                else NoteList(vm, s, editBook = { bookDialog = it }, templates = { templates = true })
                                if (s.busy) Surface(Modifier.fillMaxSize().clickable(enabled = true, onClick = {}), color = MaterialTheme.colorScheme.surface.copy(alpha = .7f)) { Box(contentAlignment = Alignment.Center) { CircularProgressIndicator() } }
                            }
                        }
                    }
                }
                if (settings != null) SettingsDialog(s, vm, activity, settings!!, { settings = null }, ::exportBackup, ::exportTexts, { importFile.launch(arrayOf("text/*", "application/octet-stream", "application/json", "*/*")) })
                if (newBook || bookDialog != null) NotebookDialog(bookDialog, vm) { newBook = false; bookDialog = null }
                if (templates) TemplatesDialog(vm) { templates = false }
                if (globalSearch) GlobalSearch(s, vm) { globalSearch = false }
                if (backup != null && s.workspace != null) PanelDialog("合并加密备份", { backup = null; backupPassword = "" }, actions = { Button(onClick = { scope.launch { restoring = true; try { vm.mergeBackup(backup!!, backupPassword); backup = null; backupPassword = "" } catch (error: Exception) { backupError = error.message ?: "恢复失败" } finally { restoring = false } } }, enabled = !restoring && backupPassword.isNotBlank()) { Text(if (restoring) "正在恢复…" else "解密并合并") } }) { Text("备份内容会合并到当前空间，冲突时保留副本。", color = MaterialTheme.colorScheme.onSurfaceVariant); Field("备份的主密码", backupPassword, secret = true) { backupPassword = it }; if (backupError.isNotBlank()) Text(backupError, color = MaterialTheme.colorScheme.error) }
            }
        }
    }
}
@Composable private fun EmptyEditor(modifier: Modifier) { Box(modifier.fillMaxHeight().background(MaterialTheme.colorScheme.surface), contentAlignment = Alignment.Center) { Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(20.dp)) { Icon(Icons.Outlined.MenuBook, null, Modifier.size(53.dp), tint = MaterialTheme.colorScheme.outline); Text("选择记录", color = MaterialTheme.colorScheme.onSurfaceVariant) } } }
@Composable private fun SideNavigation(s: AppState, vm: MojiViewModel, done: () -> Unit, settings: () -> Unit, newBook: () -> Unit, search: () -> Unit, sync: () -> Unit) {
    val w = s.workspace ?: return
    var tagsExpanded by remember { mutableStateOf(false) }
    Column(Modifier.fillMaxHeight().padding(16.dp, 24.dp)) {
        Brand(); Spacer(Modifier.height(24.dp)); OutlinedButton(onClick = { search(); done() }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(11.dp)) { Icon(Icons.Outlined.Search, null, Modifier.size(17.dp)); Spacer(Modifier.width(9.dp)); Text("搜索你的空间", style = MaterialTheme.typography.labelSmall) }; Spacer(Modifier.height(17.dp))
        Column(Modifier.weight(1f).verticalScroll(rememberScrollState())) {
            listOf(Triple("home", "工作台", Icons.Outlined.Dashboard), Triple("all", "全部记录", Icons.Outlined.Description), Triple("star", "我的收藏", Icons.Outlined.StarOutline)).forEach { (id, title, icon) -> NavigationDrawerItem(label = { Text(title, style = MaterialTheme.typography.bodyMedium) }, selected = s.view == id, onClick = { vm.navigate(id); done() }, icon = { Icon(icon, null, Modifier.size(19.dp)) }, modifier = Modifier.padding(vertical = 2.dp)) }
            Row(Modifier.padding(start = 14.dp, top = 20.dp), verticalAlignment = Alignment.CenterVertically) { Text("我的记事本", Modifier.weight(1f), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant); Tool(Icons.Outlined.Add, "新建记事本", action = newBook) }
            w.liveBooks.forEach { book -> NavigationDrawerItem(label = { Text(book.name, style = MaterialTheme.typography.bodyMedium, maxLines = 1) }, selected = s.view == book.id, onClick = { vm.navigate(book.id); done() }, icon = { Icon(modeIcon(book.mode), null, Modifier.size(19.dp), tint = color(book.color)) }, badge = { Text("${w.liveNotes.count { it.notebookId == book.id && !it.archived }}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }, modifier = Modifier.padding(vertical = 2.dp)) }
            TextButton(onClick = { tagsExpanded = !tagsExpanded }, modifier = Modifier.padding(top = 13.dp)) { Text("标签", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant); Icon(if (tagsExpanded) Icons.Outlined.ExpandLess else Icons.Outlined.ExpandMore, null, Modifier.size(16.dp)) }
            if (tagsExpanded) w.liveNotes.flatMap { it.tags }.distinct().sorted().take(20).forEach { tag -> TextButton(onClick = { vm.navigate("all"); vm.search("#$tag"); done() }, contentPadding = PaddingValues(horizontal = 14.dp, vertical = 4.dp)) { Text("#  $tag", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) } }
        }
        HorizontalDivider(Modifier.padding(vertical = 11.dp), color = MaterialTheme.colorScheme.outlineVariant)
        listOf(Triple("archive", "归档", Icons.Outlined.Archive), Triple("trash", "回收站", Icons.Outlined.DeleteOutline)).forEach { (id, title, icon) -> NavigationDrawerItem(label = { Text(title, style = MaterialTheme.typography.labelLarge) }, selected = s.view == id, onClick = { vm.navigate(id); done() }, icon = { Icon(icon, null, Modifier.size(18.dp)) }) }
        Surface(Modifier.fillMaxWidth().padding(vertical = 15.dp), shape = RoundedCornerShape(14.dp), color = MaterialTheme.colorScheme.surfaceVariant) { Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) { Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(9.dp)) { Icon(Icons.Outlined.CloudDone, null, Modifier.size(19.dp), tint = MaterialTheme.colorScheme.primary); Text(if (s.demo) "演示空间 · 不保存" else if (w.sync.lastSync > 0) "已同步 · ${Records.relative(w.sync.lastSync)}" else "本地私人空间", style = MaterialTheme.typography.labelSmall) }; TextButton(onClick = { if (w.sync.token.isNotBlank() && w.sync.code.isNotBlank() && !s.demo) vm.synchronize() else sync(); done() }, contentPadding = PaddingValues(0.dp)) { Text(if (s.syncing) "正在同步…" else if (w.sync.lastSync > 0) "立即同步  ↻" else "连接 GitHub  ↗", style = MaterialTheme.typography.labelSmall) } } }
        Row(verticalAlignment = Alignment.CenterVertically) { Text(if (s.demo) "Moji 的演示空间" else "我的私人空间", Modifier.weight(1f), style = MaterialTheme.typography.labelSmall); Tool(Icons.Outlined.Settings, "偏好设置", action = settings); Tool(Icons.Outlined.Lock, "锁定空间") { vm.lock() } }
    }
}
@Composable private fun CompactNavigation(s: AppState, vm: MojiViewModel, settings: () -> Unit, menu: () -> Unit) {
    NavigationRail(containerColor = MaterialTheme.colorScheme.surface, modifier = Modifier.fillMaxHeight().width(72.dp).verticalScroll(rememberScrollState()), header = { Tool(Icons.Outlined.Menu, "打开导航", action = menu) }) {
        NavigationRailItem(s.view == "home", { vm.navigate("home") }, icon = { Icon(Icons.Outlined.Dashboard, "工作台") })
        NavigationRailItem(s.view == "all", { vm.navigate("all") }, icon = { Icon(Icons.Outlined.Description, "全部记录") })
        s.workspace?.liveBooks?.take(8)?.forEach { book -> NavigationRailItem(s.view == book.id, { vm.navigate(book.id) }, icon = { Icon(modeIcon(book.mode), book.name) }) }
        Spacer(Modifier.height(20.dp)); Tool(Icons.Outlined.Settings, "偏好设置", action = settings); Tool(Icons.Outlined.Lock, "锁定空间") { vm.lock() }
    }
}
private fun readLimited(stream: InputStream, maximum: Int): ByteArray { val output = ByteArrayOutputStream(); val buffer = ByteArray(8192); var total = 0; var read: Int; while (stream.read(buffer).also { read = it } >= 0) { total += read; require(total <= maximum) { if (maximum == 2_000_000) "单张图片请控制在 2 MB 以内" else "导入文件请控制在 30 MB 以内" }; output.write(buffer, 0, read) }; return output.toByteArray() }
