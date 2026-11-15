package app.moji.android
import app.moji.android.data.*
object TestWorkspace {
    fun create(): Workspace { val w = Seed.create().copy(notebooks = NoteMode.entries.map { Notebook(name = "Test $it", mode = it) }); return w.copy(notes = w.notebooks.map { NoteCreation.inNotebook(w, it.id).copy(content = "test-content", title = "test-${it.mode}") }) }
}
