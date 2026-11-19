package app.moji.android.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.SaveableStateHolder
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.layout.positionInWindow
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import app.moji.android.*
import app.moji.android.data.*

@Composable fun RecordWorkspace(
    vm: MojiViewModel, s: AppState, active: Note?, activity: MainActivity,
    twoPane: Boolean, posture: WindowPosture, overviewStates: SaveableStateHolder,
    editBook: (Notebook) -> Unit, export: (Note) -> Unit, openNavigation: (() -> Unit)?
) {
    var contentX by remember { mutableFloatStateOf(0f) }
    val density = LocalDensity.current
    BoxWithConstraints(Modifier.fillMaxSize().onGloballyPositioned { contentX = it.positionInWindow().x }) {
        val seam = posture.hingeLeft?.let { with(density) { (it - contentX).toDp() } }
        val listWidth = if (posture.separating && seam != null && seam > 180.dp && seam < maxWidth - 260.dp) seam else if (maxWidth > 1200.dp) 320.dp else 276.dp
        val gap = if (posture.separating && seam == listWidth) with(density) { posture.hingeWidth.toDp().coerceAtLeast(8.dp) } else 1.dp
        key(s.view, twoPane, posture.separating) {
            if (active == null) {
                overviewStates.SaveableStateProvider("overview-${s.view}") {
                    NoteList(vm, s, Modifier.testTag("notebook-overview"), editBook, overview = true)
                }
            } else if (twoPane) {
                Row(Modifier.fillMaxSize()) {
                    NoteList(vm, s, Modifier.width(listWidth), editBook)
                    Spacer(Modifier.width(gap).fillMaxHeight().background(MaterialTheme.colorScheme.outlineVariant))
                    EditorScreen(vm, s, active, activity, export, Modifier.weight(1f))
                }
            } else {
                EditorScreen(vm, s, active, activity, export, showBack = true, back = vm::clearSelection, openNavigation = openNavigation)
            }
        }
    }
}
