package app.moji.android.data

class RecordTransferException(message: String) : IllegalArgumentException(message)
data class TransferRecord(val mode: NoteMode, val note: Note)
data class RecordBundle(val workspaceId: String, val cut: Boolean, val records: List<TransferRecord>)
data class RecordTransferResult(val state: Workspace, val ids: List<String>)

object RecordTransfer {
    fun capture(state: Workspace, selection: Collection<String>, cut: Boolean): RecordBundle {
        val ids = selection.distinct();val books=state.liveBooks.associateBy{it.id};val notes=state.notes.filter{it.deletedAt==null&&it.purgedAt==null&&it.notebookId in books}.associateBy{it.id}
        if (ids.isEmpty() || ids.any { it !in notes }) throw RecordTransferException("部分记录已不存在，请重新选择。")
        return RecordBundle(state.id, cut, ids.map { val n = notes.getValue(it); TransferRecord(books.getValue(n.notebookId).mode, n.copy(variables = n.variables.toList(), fields = n.fields.toList())) })
    }
    fun apply(state: Workspace, bundle: RecordBundle, target: String): RecordTransferResult {
        val book = state.liveBooks.firstOrNull { it.id == target } ?: throw RecordTransferException("请先打开要粘贴到的记事本。")
        if (bundle.records.isEmpty()) throw RecordTransferException("记录剪贴板已失效，请重新复制。")
        val rows = if (bundle.cut) {
            if (state.id != bundle.workspaceId) throw RecordTransferException("记录剪贴板已失效，请重新复制。")
            val books=state.liveBooks.associateBy{it.id};val fresh=state.notes.filter{it.deletedAt==null&&it.purgedAt==null&&it.notebookId in books}.associateBy{it.id}
            if (bundle.records.any { it.note.id !in fresh }) throw RecordTransferException("部分记录已不存在，请重新选择。")
            bundle.records.map { val n = fresh.getValue(it.note.id); TransferRecord(books.getValue(n.notebookId).mode, n) }
        } else bundle.records
        if (rows.any { it.mode != book.mode }) throw RecordTransferException(if (bundle.cut) "记事本类型不兼容，未移动任何记录。" else "记事本类型不兼容，未复制任何记录。")
        if (bundle.cut) {
            val ids = rows.map { it.note.id }
            if (rows.all { it.note.notebookId == target && !it.note.archived }) return RecordTransferResult(state, ids)
            return RecordTransferResult(state.copy(notes = state.notes.map { if (it.id in ids) it.copy(notebookId = target, archived = false, revision = uid()) else it }), ids)
        }
        if (state.notes.size + rows.size > 100000) throw RecordTransferException("记录数量已达上限。")
        val names = state.notes.filter { it.notebookId == target&&it.deletedAt==null&&it.purgedAt==null }.map { it.title }.toMutableSet()
        val copies = rows.map { row ->
            val n = row.note; var title = "${n.title} · 副本"; var index = 2
            while (title in names) title = "${n.title} · 副本 ${index++}"
            names += title
            n.copy(id = uid(), notebookId = target, title = title, variables = n.variables.toList(), fields = n.fields.toList(), pinned = false, favorite = false, archived = false, deletedAt = null, purgedAt = null, history = emptyList(), createdAt = now(), updatedAt = now(), revision = uid())
        }
        return RecordTransferResult(state.copy(notes = copies + state.notes), copies.map { it.id })
    }
}
