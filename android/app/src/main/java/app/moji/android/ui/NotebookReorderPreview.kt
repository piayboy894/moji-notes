package app.moji.android.ui

import androidx.compose.foundation.interaction.DragInteraction
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.runtime.*

@Stable internal class NotebookReorderPreview(initial: List<String>) {
    var order by mutableStateOf(initial); private set
    var active by mutableStateOf(false); private set
    private var saved = initial
    fun sync(ids: List<String>) { if (ids != saved) { saved = ids; order = ids; active = false } }
    fun begin() { active = true }
    fun move(source: String, target: String) {
        if (!active || source == target || source !in order || target !in order) return
        val from = order.indexOf(source); val to = order.indexOf(target)
        order = order.toMutableList().apply { removeAt(from); add(to, source) }
    }
    fun cancel() { order = saved; active = false }
    fun finish(commit: (List<String>) -> Unit) {
        if (!active) return
        active = false
        if (order != saved) { saved = order; commit(order) }
    }
}

@Composable internal fun rememberNotebookReorderPreview(workspace: String, ids: List<String>, commit: (List<String>) -> Unit): Pair<NotebookReorderPreview, MutableInteractionSource> {
    val preview = remember(workspace) { NotebookReorderPreview(ids) }
    val interactions = remember(workspace) { MutableInteractionSource() }
    val latestCommit by rememberUpdatedState(commit)
    LaunchedEffect(ids) { preview.sync(ids) }
    LaunchedEffect(interactions) {
        interactions.interactions.collect { event ->
            when (event) {
                is DragInteraction.Stop -> preview.finish(latestCommit)
                is DragInteraction.Cancel -> preview.cancel()
            }
        }
    }
    DisposableEffect(preview) { onDispose { preview.cancel() } }
    return preview to interactions
}
