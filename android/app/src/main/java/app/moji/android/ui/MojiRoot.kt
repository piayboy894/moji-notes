package app.moji.android.ui

import androidx.compose.runtime.saveable.rememberSaveableStateHolder
import androidx.compose.animation.core.tween
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import kotlinx.coroutines.flow.collectLatest
import android.app.Activity
import android.content.Intent
import android.net.Uri
import android.provider.OpenableColumns
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import sh.calvin.reorderable.*
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.foundation.focusable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.input.key.*
import androidx.compose.ui.input.pointer.*
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.layout.positionInWindow
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.semantics.testTagsAsResourceId
import androidx.compose.ui.unit.dp
import androidx.core.view.WindowCompat
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.moji.android.*
import app.moji.android.data.*
import app.moji.android.security.Envelope
import kotlinx.serialization.json.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.ByteArrayOutputStream
import java.io.InputStream
import java.io.OutputStream
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream

@OptIn(ExperimentalMaterial3Api::class, androidx.compose.ui.ExperimentalComposeUiApi::class)
@Composable fun MojiRoot(vm: MojiViewModel, activity: MainActivity) {
    val s by vm.state.collectAsStateWithLifecycle(); val posture by activity.posture.collectAsStateWithLifecycle()
    var cloudSaves by remember { mutableStateOf(false) }; var settings by remember { mutableStateOf<String?>(null) }; var bookDialog by remember { mutableStateOf<Notebook?>(null) }; var newBook by remember { mutableStateOf(false) }; var globalSearch by remember { mutableStateOf(false) };var searchRequest by remember{mutableIntStateOf(0)};var searchTarget by remember{mutableStateOf("")};var searchOwner by remember{mutableStateOf<String?>(null)}
    var exportTarget by remember { mutableStateOf<Note?>(null) }
    var plainBackup by remember { mutableStateOf<SyncPayload?>(null) }
    var backup by remember { mutableStateOf<Envelope?>(null) }; var backupPassword by remember { mutableStateOf("") }; var backupError by remember { mutableStateOf("") }; var restoring by remember { mutableStateOf(false) }
    var exportWriter by remember { mutableStateOf<(suspend (OutputStream) -> Unit)?>(null) }
    val canCreateRecord = s.workspace?.liveBooks?.any { it.id == s.view } == true
    fun addRecord() { if (canCreateRecord) vm.addNote(s.view) }
    val keyboardFocus=remember{FocusRequester()}
    LaunchedEffect(s.workspace!=null,s.view,s.selectedId,settings,cloudSaves){if(s.workspace!=null&&s.selectedId==null&&settings==null&&!cloudSaves&&!globalSearch)runCatching{keyboardFocus.requestFocus()}}
    val overviewStates=rememberSaveableStateHolder()
    val scope = rememberCoroutineScope(); val snackbar = remember { SnackbarHostState() }; val drawer = rememberDrawerState(DrawerValue.Closed)
    val layers = remember { FeedbackLayers(snackbar) }
    val density = LocalDensity.current
    val drawerVisible by remember(density) { derivedStateOf {
        drawer.isOpen || drawer.targetValue == DrawerValue.Open ||
            (drawer.currentOffset.isFinite() && drawer.currentOffset > -with(density) { 280.dp.toPx() } + 1f)
    } }
    val focusManager = LocalFocusManager.current
    val keyboard = LocalSoftwareKeyboardController.current
    LaunchedEffect(drawerVisible) { if (drawerVisible) { focusManager.clearFocus(force = true); keyboard?.hide() } }
    fun name(uri: Uri): String = runCatching { activity.contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { if (it.moveToFirst()) it.getString(0) else null } }.getOrNull() ?: "导入记录"
    val importFile = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri -> if (uri != null) scope.launch {
        try { val fileName = name(uri); val text = withContext(Dispatchers.IO) { activity.contentResolver.openInputStream(uri)?.use { readLimited(it, 30_000_000).toString(Charsets.UTF_8) } ?: error("无法读取文件") }
            if (fileName.endsWith(".moji", true) || fileName.endsWith(".mojibak", true) || fileName.endsWith(".json", true)) { if (json.parseToJsonElement(text).jsonObject["format"]?.jsonPrimitive?.content == "moji-encrypted") { backup = VaultStore.decodeEnvelope(text); backupPassword = ""; backupError = "" } else { val payload = json.decodeFromString<SyncPayload>(text); app.moji.android.sync.GameSaves.apply(Workspace(),payload); plainBackup = payload } }
            else if (s.workspace == null) vm.message("本机数据尚未打开") else vm.importText(fileName.substringBeforeLast('.', fileName),text,markdown=fileName.endsWith(".md",true)||fileName.endsWith(".markdown",true))
        } catch (error: Exception) { vm.message(UserMessages.forError(error,"导入失败")) }
    } }
    val createFile = rememberLauncherForActivityResult(ActivityResultContracts.StartActivityForResult()) { result -> val writer = exportWriter; exportWriter = null; val uri = result.data?.data
        if (result.resultCode == Activity.RESULT_OK && uri != null && writer != null) scope.launch { try { withContext(Dispatchers.IO) { activity.contentResolver.openOutputStream(uri)?.use { writer(it) } ?: error("无法写入目标文件") }; vm.message("导出完成") } catch (error: Exception) { vm.message(UserMessages.forError(error,"导出失败")) } }
    }
    fun createDocument(fileName: String, mime: String, writer: suspend (OutputStream) -> Unit) { exportWriter = writer; createFile.launch(Intent(Intent.ACTION_CREATE_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE).setType(mime).putExtra(Intent.EXTRA_TITLE, fileName)) }
    fun exportNote(note: Note) { exportTarget = note }
    fun exportBackup() { scope.launch { try { val content = vm.localBackup(); createDocument("墨记-存档-${java.time.LocalDate.now()}.moji", "application/octet-stream") { it.write(content.toByteArray(Charsets.UTF_8)) } } catch (error: Exception) { vm.message(UserMessages.forError(error,"备份失败")) } } }
    fun exportTexts() { val w = s.workspace ?: return; createDocument("墨记-文本集-${java.time.LocalDate.now()}.zip", "application/zip") { stream -> ZipOutputStream(stream).use { zip -> w.liveNotes.forEach { note -> val b = w.notebooks.first { it.id == note.notebookId }; if (b.mode != NoteMode.Password) { zip.putNextEntry(ZipEntry("${Records.safeName(b.name)}/${Records.safeName(note.title)}-${note.id.take(6)}.txt")); zip.write("${note.title}\n\n${visibleText(activity,note)}".toByteArray(Charsets.UTF_8)); zip.closeEntry() } } } } }
    LaunchedEffect(s.view) { vm.editorActions.collect { action -> when(action){"image-error"->vm.message("图片无法插入，请换一张图片重试");"search"->globalSearch=true;"cloud"->cloudSaves=true;"new"->addRecord();"save"->{vm.flush();vm.selectedNote()?.let{vm.checkpoint(it.id)}}} } }
    var recordDragNeedsDrawer by remember{mutableStateOf(true)}
    LaunchedEffect(vm){vm.recordFocusRequests.collect{keyboardFocus.requestFocus()}}
    LaunchedEffect(vm){var opened=false;vm.recordDrags.collect{started->if(started){keyboardFocus.requestFocus();if(recordDragNeedsDrawer){opened=true;drawer.open()}}else if(opened){opened=false;drawer.close()}}}
    LaunchedEffect(Unit) { vm.messages.collectLatest { snackbar.currentSnackbarData?.dismiss();snackbar.showSnackbar(it) } }
    LaunchedEffect(s.workspace == null) { if (s.workspace == null) { cloudSaves = false; settings = null; bookDialog = null; newBook = false; globalSearch = false; backup = null; exportWriter = null } else { backup = null } }
    val dark = s.workspace?.preferences?.theme == "dark" || ((s.workspace?.preferences?.theme ?: "system") == "system" && isSystemInDarkTheme())
    SideEffect { WindowCompat.getInsetsController(activity.window, activity.window.decorView).apply { isAppearanceLightStatusBars = !dark; isAppearanceLightNavigationBars = !dark }; val selected = s.workspace?.notes?.firstOrNull { it.id == s.selectedId }; activity.secure((selected != null && s.workspace?.notebooks?.firstOrNull { it.id == selected.notebookId }?.mode == NoteMode.Password) || settings != null || backup != null) }
    MojiTheme(s.workspace?.preferences ?: Preferences()) {
        CompositionLocalProvider(LocalFeedbackLayers provides layers, LocalEditorObscured provides (drawerVisible || layers.dialogOpen || searchOwner!=null)) {
        Surface(Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
            BoxWithConstraints(Modifier.fillMaxSize().recordDragRoot(vm).focusRequester(keyboardFocus).semantics { testTagsAsResourceId = true }.safeDrawingPadding().imePadding().onPreviewKeyEvent { event ->
                if (s.workspace == null || event.type != KeyEventType.KeyDown || settings != null || cloudSaves || newBook || bookDialog != null || globalSearch || searchOwner!=null || backup != null) false
                else when (Shortcuts.resolve(s.workspace!!.preferences, keyChord(event))) {
                    "recordCopy" -> if(vm.recordKeyboardScope){vm.copyRecords(vm.recordsForCommand(),false);true}else false
                    "recordCut" -> if(vm.recordKeyboardScope){vm.copyRecords(vm.recordsForCommand(),true);true}else false
                    "recordPaste" -> if(vm.recordKeyboardScope){vm.pasteRecords();true}else false
                    "search" -> { globalSearch = true; true }
                    "new" -> { if (canCreateRecord) { addRecord(); true } else false }
                    "cloud" -> { cloudSaves = true; true }
                    "save" -> { vm.selectedNote()?.let { vm.checkpoint(it.id) }; true }
                    else -> false
                }
            }.focusable()) {
                if (s.workspace == null) { StartupScreen(s, vm); ForegroundFeedback(modifier = Modifier.align(Alignment.BottomCenter)); return@BoxWithConstraints }
                val stableHeight=maxHeight+with(density){(WindowInsets.ime.getBottom(density)-WindowInsets.navigationBars.getBottom(density)).coerceAtLeast(0).toDp()};val wide = maxWidth >= 1100.dp && stableHeight >= 480.dp; val rail = !wide && maxWidth >= 720.dp && stableHeight >= 480.dp
                LaunchedEffect(globalSearch,wide){if(globalSearch){searchTarget=if(wide)"wide"else"drawer";if(!wide)drawer.open();searchRequest++;globalSearch=false}}
                SideEffect{recordDragNeedsDrawer=!wide}
                val navigationWidth = if (wide) 224.dp else if (rail) 72.dp else 0.dp
                val twoPane = maxWidth - navigationWidth >= 680.dp && stableHeight >= 480.dp
                val active = s.workspace?.notes?.firstOrNull { it.id == s.selectedId && it.purgedAt == null }
                BackHandler(enabled = active != null || s.view != "home") { if (active != null) vm.clearSelection() else vm.navigate("home") }
                ModalNavigationDrawer(drawerState = drawer, gesturesEnabled = !wide && !rail, drawerContent = { ModalDrawerSheet(Modifier.width(280.dp), drawerContainerColor = MaterialTheme.colorScheme.surfaceContainerLow) { SideNavigation(s,vm,done={scope.launch{drawer.close()}},settings={settings="general"},newBook={newBook=true},sync={cloudSaves=true},searchRequest=if(searchTarget=="drawer")searchRequest else 0,searchActive=drawerVisible&&!wide,searchVisible={if(it)searchOwner="drawer"else if(searchOwner=="drawer")searchOwner=null}) } }) {
                    Row(Modifier.fillMaxSize().semantics { stateDescription = if(active==null&&s.view !in listOf("home","trash"))"卡片网格"else if (wide && twoPane && s.view != "home") "三栏布局" else if (twoPane && s.view != "home") "双栏布局" else "单栏布局" }.testTag("adaptive-layout")) {
                        if (wide) { Surface(Modifier.width(224.dp).fillMaxHeight().testTag("wide-navigation"), color = MaterialTheme.colorScheme.surfaceContainerLow) { SideNavigation(s,vm,done={},settings={settings="general"},newBook={newBook=true},sync={cloudSaves=true},searchRequest=if(searchTarget=="wide")searchRequest else 0,searchActive=true,searchVisible={if(it)searchOwner="wide"else if(searchOwner=="wide")searchOwner=null}) }; VerticalDivider(color = MaterialTheme.colorScheme.outlineVariant) }
                        else if (rail) { CompactNavigation(s, vm, { settings = "general" }, { scope.launch { drawer.open() } }); VerticalDivider(color = MaterialTheme.colorScheme.outlineVariant) }
                        Scaffold(Modifier.weight(1f).fillMaxHeight(), containerColor = MaterialTheme.colorScheme.background,
                            topBar = { if (twoPane || active == null || s.view == "home") TopAppBar(title = { Text(viewTitle(s), style = MaterialTheme.typography.titleMedium, maxLines = 1) }, navigationIcon = { if (!wide && !rail) Tool(MojiIcons.Menu, "打开导航") { scope.launch { drawer.open() } } }, actions = { if (s.saving) CircularProgressIndicator(Modifier.size(15.dp), strokeWidth = 2.dp) else if (s.saveError) Icon(MojiIcons.ErrorOutline, "保存失败", tint = MaterialTheme.colorScheme.error) ; Tool(MojiIcons.Search, "全局搜索") { globalSearch = true }; if (twoPane && canCreateRecord) Tool(MojiIcons.Add, "新建记录") { addRecord() } }, colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background)) },
                            floatingActionButton = { if (!twoPane && active == null && canCreateRecord) FloatingActionButton(onClick = { addRecord() }, containerColor = MaterialTheme.colorScheme.primary) { Icon(MojiIcons.Add, "新建记录", tint = MaterialTheme.colorScheme.onPrimary) } }
                        ) { padding ->
                            Box(Modifier.padding(padding).fillMaxSize()) {
                                if (s.view == "home") HomeScreen(vm, s, { newBook = true }, { bookDialog = it })
                                else if(s.view=="trash")TrashScreen(vm,s)
                                else RecordWorkspace(vm,s,active,activity,twoPane,posture,overviewStates,editBook={bookDialog=it},export=::exportNote)
                                if (s.busy) Surface(Modifier.fillMaxSize().clickable(enabled = true, onClick = {}), color = MaterialTheme.colorScheme.surface.copy(alpha = .7f)) { Box(contentAlignment = Alignment.Center) { CircularProgressIndicator() } }
                            }
                        }
                    }
                }
                ForegroundFeedback(modifier = Modifier.align(Alignment.BottomCenter).padding(bottom = if (drawerVisible) 72.dp else 0.dp))
                exportTarget?.let { note -> PanelDialog("导出记录",{exportTarget=null}) {
                    TextButton(onClick={exportTarget=null;val text=visibleText(activity,note);createDocument(Records.safeName(note.title)+".txt","text/plain"){it.write(text.toByteArray(Charsets.UTF_8))}}){Text("普通文本 (.txt)")}
                    TextButton(onClick={exportTarget=null;val text="<!doctype html><meta charset=\"utf-8\">"+EditorContent.html(PromptVariables.fill(note,note.variables.associate{it.id to it.name}));createDocument(Records.safeName(note.title)+".html","text/html"){it.write(text.toByteArray(Charsets.UTF_8))}}){Text("HTML (.html)")}
                } }
                if (settings != null) SettingsDialog(s, vm, activity, settings!!, { settings = null }, ::exportBackup, ::exportTexts, { importFile.launch(arrayOf("text/*", "application/octet-stream", "application/json", "*/*")) }, { settings = null; cloudSaves = true })
                if (cloudSaves) CloudSavesDialog(vm, s, { cloudSaves = false }, { cloudSaves = false; settings = "sync" })
                if (newBook || bookDialog != null) NotebookDialog(bookDialog, vm) { newBook = false; bookDialog = null }
                if (s.textImport != null) PanelDialog("导入到记事本",vm::cancelImport) {
                    s.workspace!!.liveBooks.filter{it.mode!=NoteMode.Password}.forEach{book->TextButton(onClick={vm.importText(s.textImport!!.name,s.textImport!!.text,book.id,s.textImport!!.markdown)}){Text(book.name)}}
                    if(s.workspace!!.liveBooks.none{it.mode!=NoteMode.Password})Text("请先新建文本或提示词记事本")
                }
                if (plainBackup != null) Confirm("读取这份存档？","将替换本机全部记事本和记录。",{plainBackup=null}) { val payload=plainBackup!!;scope.launch{try{vm.replaceData(payload)}catch(error:Exception){vm.message(UserMessages.forError(error,"读取失败"))}} }
                if (backup != null && s.workspace != null) PanelDialog("读取旧版备份", { backup = null; backupPassword = "" }, actions = { Button(onClick = { scope.launch { restoring = true; try { vm.readBackup(backup!!, backupPassword); backup = null; backupPassword = "" } catch (error: Exception) { backupError = UserMessages.forError(error,"恢复失败") } finally { restoring = false } } }, enabled = !restoring && backupPassword.isNotBlank()) { Text(if (restoring) "正在恢复…" else "确认读取") } }) { Text("将替换本机全部记事本和记录，不合并。", color = MaterialTheme.colorScheme.onSurfaceVariant); Field("旧备份密码", backupPassword, secret = true) { backupPassword = it }; if (backupError.isNotBlank()) Text(backupError, color = MaterialTheme.colorScheme.error) }
            }
        }
        }
    }
}
@Composable private fun EmptyEditor(modifier: Modifier) { Box(modifier.fillMaxHeight().background(MaterialTheme.colorScheme.surface), contentAlignment = Alignment.Center) { Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(20.dp)) { Icon(MojiIcons.MenuBook, null, Modifier.size(53.dp), tint = MaterialTheme.colorScheme.outline); Text("选择记录", color = MaterialTheme.colorScheme.onSurfaceVariant) } } }
@Composable private fun SideNavigation(s: AppState, vm: MojiViewModel, done: () -> Unit, settings: () -> Unit, newBook: () -> Unit, sync: () -> Unit,searchRequest:Int,searchActive:Boolean,searchVisible:(Boolean)->Unit) {
    val w = s.workspace ?: return
    val listState=rememberLazyListState();val haptic=LocalHapticFeedback.current
    val (preview,dragInteractions)=rememberNotebookReorderPreview(w.id,w.liveBooks.map{it.id},vm::setBookOrder)
    val booksById=w.liveBooks.associateBy{it.id}
    val reordering=rememberReorderableLazyListState(listState){from,to->preview.move(from.key.toString(),to.key.toString())}
    Column(Modifier.fillMaxHeight().padding(16.dp,24.dp)) {
        Brand();Spacer(Modifier.height(24.dp));GlobalSearchField(s,vm,searchRequest,searchActive,done,searchVisible);Spacer(Modifier.height(17.dp))
        LazyColumn(Modifier.weight(1f).testTag("notebook-sidebar-list"),state=listState){
            item(key="navigation"){listOf(Triple("home","工作台",MojiIcons.Dashboard),Triple("all","全部记录",MojiIcons.Description),Triple("star","我的收藏",MojiIcons.StarOutline)).forEach{(id,title,icon)->NavigationDrawerItem(colors=solidNavigationColors(),label={Text(title,style=MaterialTheme.typography.bodyMedium)},selected=s.view==id,onClick={vm.navigate(id);done()},icon={Icon(icon,null,Modifier.size(19.dp))},modifier=Modifier.padding(vertical=2.dp))}}
            item(key="notebooks-label"){Row(Modifier.padding(start=14.dp,top=20.dp),verticalAlignment=Alignment.CenterVertically){Text("我的记事本",Modifier.weight(1f),style=MaterialTheme.typography.labelSmall,color=MaterialTheme.colorScheme.onSurfaceVariant);Tool(MojiIcons.Add,"新建记事本"){newBook();done()}}}
            items(preview.order.mapNotNull{booksById[it]},key={it.id}){book->ReorderableItem(reordering,key=book.id,animateItemModifier=Modifier.animateItem(fadeInSpec=null,placementSpec=tween(150),fadeOutSpec=null)){dragging->NavigationDrawerItem(colors=solidNavigationColors(),label={Text(book.name,style=MaterialTheme.typography.bodyMedium,maxLines=1)},selected=s.view==book.id,onClick={vm.navigate(book.id);vm.focusRecords();done()},icon={Icon(notebookIcon(book),null,Modifier.size(19.dp),tint=if(s.view==book.id)color(LocalMojiPalette.current.colors.getValue("RecordSelectedInk"))else color(book.color))},badge={Text("${w.liveNotes.count{it.notebookId==book.id&&!it.archived}}",style=MaterialTheme.typography.labelSmall,color=if(s.view==book.id)color(LocalMojiPalette.current.colors.getValue("RecordSelectedSub"))else MaterialTheme.colorScheme.onSurfaceVariant)},modifier=Modifier.padding(vertical=2.dp).recordDropTarget(vm,book).testTag("sidebar-book-${book.id}").longPressDraggableHandle(interactionSource=dragInteractions,onDragStarted={preview.begin();haptic.performHapticFeedback(HapticFeedbackType.LongPress)}))}}
            item(key="management"){
        HorizontalDivider(Modifier.padding(vertical = 11.dp), color = MaterialTheme.colorScheme.outlineVariant)
        listOf(Triple("archive", "归档", MojiIcons.Archive), Triple("trash", "回收站", MojiIcons.DeleteOutline)).forEach { (id, title, icon) -> NavigationDrawerItem(colors=solidNavigationColors(),label = { Text(title, style = MaterialTheme.typography.labelLarge) }, selected = s.view == id, onClick = { vm.navigate(id); done() }, icon = { Icon(icon, null, Modifier.size(18.dp)) }) }
            }
        }
        HorizontalDivider(Modifier.padding(top=12.dp,bottom=8.dp),color=MaterialTheme.colorScheme.outlineVariant)
        Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(4.dp)){
            TextButton(onClick={sync();done()},modifier=Modifier.weight(1f).height(44.dp).testTag("cloud-saves-entry"),contentPadding=PaddingValues(8.dp,6.dp)){Icon(MojiIcons.CloudDone,null,Modifier.size(20.dp));Spacer(Modifier.width(8.dp));Text("云存档",style=MaterialTheme.typography.bodyMedium,color=MaterialTheme.colorScheme.onSurface)}
            TextButton(onClick={settings();done()},modifier=Modifier.weight(1f).height(44.dp),contentPadding=PaddingValues(8.dp,6.dp)){Icon(MojiIcons.Settings,"偏好设置",Modifier.size(20.dp),tint=MaterialTheme.colorScheme.onSurfaceVariant);Spacer(Modifier.width(8.dp));Text("设置",style=MaterialTheme.typography.bodyMedium,color=MaterialTheme.colorScheme.onSurface)}
        }
    }
}
@Composable private fun CompactNavigation(s: AppState, vm: MojiViewModel, settings: () -> Unit, menu: () -> Unit) {
    NavigationRail(containerColor = MaterialTheme.colorScheme.surface, modifier = Modifier.fillMaxHeight().width(72.dp).verticalScroll(rememberScrollState()), header = { Tool(MojiIcons.Menu, "打开导航", action = menu) }) {
        NavigationRailItem(s.view == "home", { vm.navigate("home") }, icon = { Icon(MojiIcons.Dashboard, "工作台") })
        NavigationRailItem(s.view == "all", { vm.navigate("all") }, icon = { Icon(MojiIcons.Description, "全部记录") })
        s.workspace?.liveBooks?.take(8)?.forEach { book -> NavigationRailItem(s.view == book.id, { vm.navigate(book.id) }, icon = { Icon(notebookIcon(book), book.name,tint=if(s.view==book.id)color(LocalMojiPalette.current.colors.getValue("RecordSelectedInk"))else color(book.color)) }) }
        Spacer(Modifier.height(20.dp)); Tool(MojiIcons.Settings, "偏好设置", action = settings)
    }
}
private fun readLimited(stream: InputStream, maximum: Int): ByteArray { val output = ByteArrayOutputStream(); val buffer = ByteArray(8192); var total = 0; var read: Int; while (stream.read(buffer).also { read = it } >= 0) { total += read; require(total <= maximum) { if (maximum == 2_000_000) "单张图片请控制在 2 MB 以内" else "导入文件请控制在 30 MB 以内" }; output.write(buffer, 0, read) }; return output.toByteArray() }

@Composable private fun solidNavigationColors()=NavigationDrawerItemDefaults.colors(selectedContainerColor=color(LocalMojiPalette.current.colors.getValue("RecordSelected")),selectedTextColor=color(LocalMojiPalette.current.colors.getValue("RecordSelectedInk")),selectedIconColor=color(LocalMojiPalette.current.colors.getValue("RecordSelectedInk")),selectedBadgeColor=color(LocalMojiPalette.current.colors.getValue("RecordSelectedSub")))
