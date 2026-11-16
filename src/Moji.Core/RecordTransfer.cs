namespace Moji.Core;

public sealed class RecordTransferException(string message) : Exception(message);
public record TransferRecord(NoteMode Mode, Note Note);
public record RecordBundle(string WorkspaceId, bool Cut, List<TransferRecord> Records);
public record RecordTransferResult(Workspace State, List<string> Ids);

public static class RecordTransfer
{
    public static RecordBundle Capture(Workspace state, IEnumerable<string> selection, bool cut)
    {
        var ids = selection.Distinct().ToList(); var books = state.LiveBooks.ToDictionary(b => b.Id);var notes=state.Notes.Where(n=>n.DeletedAt==null&&n.PurgedAt==null&&books.ContainsKey(n.NotebookId)).ToDictionary(n=>n.Id);
        if (ids.Count == 0 || ids.Any(id => !notes.ContainsKey(id))) throw new RecordTransferException("部分记录已不存在，请重新选择。");
        return new(state.Id, cut, ids.Select(id => new TransferRecord(books[notes[id].NotebookId].Mode, notes[id] with { Variables = [.. notes[id].Variables], Fields = [.. notes[id].Fields] })).ToList());
    }
    public static RecordTransferResult Apply(Workspace state, RecordBundle bundle, string target)
    {
        var book = state.LiveBooks.FirstOrDefault(b => b.Id == target) ?? throw new RecordTransferException("请先打开要粘贴到的记事本。");
        if (bundle.Records.Count == 0) throw new RecordTransferException("记录剪贴板已失效，请重新复制。");
        var rows = bundle.Records;
        if (bundle.Cut) {
            if (state.Id != bundle.WorkspaceId) throw new RecordTransferException("记录剪贴板已失效，请重新复制。");
            var books = state.LiveBooks.ToDictionary(b => b.Id);var fresh=state.Notes.Where(n=>n.DeletedAt==null&&n.PurgedAt==null&&books.ContainsKey(n.NotebookId)).ToDictionary(n=>n.Id);
            if (rows.Any(r => !fresh.ContainsKey(r.Note.Id))) throw new RecordTransferException("部分记录已不存在，请重新选择。");
            rows = rows.Select(r => new TransferRecord(books[fresh[r.Note.Id].NotebookId].Mode, fresh[r.Note.Id])).ToList();
        }
        if (rows.Any(r => r.Mode != book.Mode)) throw new RecordTransferException(bundle.Cut ? "记事本类型不兼容，未移动任何记录。" : "记事本类型不兼容，未复制任何记录。");
        if (bundle.Cut) {
            var ids = rows.Select(r => r.Note.Id).ToHashSet();
            if (rows.All(r => r.Note.NotebookId == target && !r.Note.Archived)) return new(state, ids.ToList());
            return new(state with { Notes = state.Notes.Select(n => ids.Contains(n.Id) ? n with { NotebookId = target, Archived = false, Revision = Clock.Id } : n).ToList() }, ids.ToList());
        }
        if (state.Notes.Count + rows.Count > 100000) throw new RecordTransferException("记录数量已达上限。");
        var names = state.Notes.Where(n => n.NotebookId == target&&n.DeletedAt==null&&n.PurgedAt==null).Select(n => n.Title).ToHashSet(); var copies = new List<Note>();
        foreach (var row in rows) {
            var note = row.Note; var title = note.Title + " · 副本";
            for (var i = 2; names.Contains(title); i++) title = note.Title + " · 副本 " + i;
            names.Add(title);
            copies.Add(note with { Id = Clock.Id, NotebookId = target, Title = title, Variables = [.. note.Variables], Fields = [.. note.Fields], Pinned = false, Favorite = false, Archived = false, DeletedAt = null, PurgedAt = null, History = [], CreatedAt = Clock.Now, UpdatedAt = Clock.Now, Revision = Clock.Id });
        }
        return new(state with { Notes = [.. copies, .. state.Notes] }, copies.Select(n => n.Id).ToList());
    }
}
