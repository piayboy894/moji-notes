package app.moji.android.ui

import androidx.compose.animation.*
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.SaveableStateHolder
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.layout.positionInWindow
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import app.moji.android.*
import app.moji.android.data.*

@OptIn(ExperimentalSharedTransitionApi::class)
@Stable class RecordSharedMotion(val shared: SharedTransitionScope, val visibility: AnimatedVisibilityScope)

/** Only viewport cards join the transition. ScaleToBounds keeps text measured
 * once at its final constraints instead of reflowing it on every frame. */
@OptIn(ExperimentalSharedTransitionApi::class)
@Composable fun Modifier.recordSharedBounds(key: String, motion: RecordSharedMotion?, enabled: Boolean = true, z: Float = 1f, crop: Boolean = false): Modifier {
    if (motion == null || !enabled) return this
    return with(motion.shared) {
        this@recordSharedBounds.sharedBounds(
            rememberSharedContentState(key), animatedVisibilityScope = motion.visibility,
            boundsTransform = { _, _ -> tween(275, easing = FastOutSlowInEasing) },
            enter = EnterTransition.None, exit = ExitTransition.None,
            resizeMode = SharedTransitionScope.ResizeMode.ScaleToBounds(if(crop)ContentScale.Crop else ContentScale.FillWidth, androidx.compose.ui.Alignment.TopStart), zIndexInOverlay = z
        )
    }
}

@OptIn(ExperimentalSharedTransitionApi::class)
@Composable fun RecordWorkspace(
    vm: MojiViewModel, s: AppState, active: Note?, activity: MainActivity,
    twoPane: Boolean, posture: WindowPosture, overviewStates: SaveableStateHolder,
    editBook: (Notebook) -> Unit, export: (Note) -> Unit
) {
    var contentX by remember { mutableFloatStateOf(0f) }
    val density = LocalDensity.current
    BoxWithConstraints(Modifier.fillMaxSize().onGloballyPositioned { contentX = it.positionInWindow().x }) {
        val seam = posture.hingeLeft?.let { with(density) { (it - contentX).toDp() } }
        val listWidth = if (posture.separating && seam != null && seam > 180.dp && seam < maxWidth - 260.dp) seam else if (maxWidth > 1200.dp) 320.dp else 276.dp
        val gap = if (posture.separating && seam == listWidth) with(density) { posture.hingeWidth.toDp().coerceAtLeast(8.dp) } else 1.dp
        // Record changes keep this same scene/editor. Only overview <-> detail
        // changes the target; resizing or changing notebooks cancels old motion.
        key(s.view, twoPane, posture.separating) {
            SharedTransitionLayout(Modifier.fillMaxSize().clipToBounds()) {
                AnimatedContent(
                    targetState = active != null,
                    modifier = Modifier.fillMaxSize(), label = "record-workspace",
                    transitionSpec = { (EnterTransition.None togetherWith ExitTransition.None).using(null) }
                ) { editing ->
                    val motion = if (twoPane && (active != null || !editing)) RecordSharedMotion(this@SharedTransitionLayout, this@AnimatedContent) else null
                    if (!editing) {
                        overviewStates.SaveableStateProvider("overview-${s.view}") {
                            val gridMotion = if(active!=null)Modifier.animateEnterExit(enter=EnterTransition.None,exit=slideOutHorizontally(tween(100)) { -48 } + fadeOut(tween(100)))else Modifier
                            NoteList(vm, s, gridMotion.testTag("notebook-overview"), editBook, overview = true, sharedMotion = motion)
                        }
                    } else if (twoPane) {
                        Row(Modifier.fillMaxSize()) {
                            NoteList(vm, s, Modifier.width(listWidth).animateEnterExit(enter=fadeIn(tween(85, delayMillis=275)),exit=ExitTransition.None), editBook, sharedMotion = motion)
                            if (active != null) {
                                // One opaque moving pane, including its separator and
                                // background. It occludes the card transition overlay.
                                Row(Modifier.weight(1f).fillMaxHeight()
                                    .renderInSharedTransitionScopeOverlay(zIndexInOverlay=10f)
                                    .animateEnterExit(enter=slideInHorizontally(tween(315, delayMillis=45, easing=FastOutSlowInEasing)) { it },exit=ExitTransition.None)
                                    .background(MaterialTheme.colorScheme.surface).testTag("entering-editor-pane")) {
                                    Spacer(Modifier.width(gap).fillMaxHeight().background(MaterialTheme.colorScheme.outlineVariant))
                                    EditorScreen(vm, s, active, activity, export, Modifier.weight(1f))
                                }
                            }
                        }
                    } else if (active != null) {
                        EditorScreen(vm, s, active, activity, export,
                            Modifier.animateEnterExit(enter=slideInHorizontally(tween(280,easing=FastOutSlowInEasing)) { it },exit=ExitTransition.None),
                            showBack = true, back = vm::clearSelection)
                    }
                }
            }
        }
    }
}
