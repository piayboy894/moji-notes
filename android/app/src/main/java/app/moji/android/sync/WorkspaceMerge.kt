package app.moji.android.sync

import app.moji.android.data.*

data class MergeResult(val notebooks: List<Notebook>, val notes: List<Note>, val conflicts: Int)
object WorkspaceMerge {
    fun revisions(v: Workspace) = v.notebooks.associate { it.id to it.revision } + v.notes.associate { it.id to it.revision }
    fun merge(local: Workspace, remote: SyncPayload, base: Map<String, String>): MergeResult {
        val books = local.notebooks.associateBy { it.id }.toMutableMap()
        remote.notebooks.forEach { r -> if (books[r.id] == null || r.updatedAt > books.getValue(r.id).updatedAt) books[r.id] = r }
        val notes = local.notes.associateBy { it.id }.toMutableMap(); var conflicts = 0
        remote.notes.forEach { r ->
            val l = notes[r.id]
            if (l == null) notes[r.id] = r
            else if (l.revision != r.revision) {
                val lChanged = l.revision != base[l.id]; val rChanged = r.revision != base[r.id]
                if (lChanged && rChanged) {
                    val winner = when { l.purgedAt != null -> l; r.purgedAt != null -> r; l.deletedAt != null -> l; r.deletedAt != null -> r; else -> l }
                    val other = if (winner === l) r else l; notes[r.id] = winner
                    if (other.purgedAt == null && other.deletedAt == null) { val copy = other.copy(id = uid(), title = "${other.title} · 同步副本", revision = uid(), updatedAt = now(), deletedAt = null, purgedAt = null); notes[copy.id] = copy; conflicts++ }
                } else if (rChanged) notes[r.id] = r
            }
        }
        val deletedBooks = books.values.filter { it.deletedAt != null }.map { it.id }.toSet()
        notes.values.toList().filter { it.notebookId in deletedBooks && it.deletedAt == null }.forEach { notes[it.id] = it.copy(deletedAt = now(), updatedAt = now(), revision = uid()) }
        return MergeResult(books.values.toList(), notes.values.toList(), conflicts)
    }
}
