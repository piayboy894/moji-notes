using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Windows.Threading;
using Moji.Core;

namespace Moji.App;
public sealed class WorkspaceViewModel : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? DataChanged;
    public event Action<string>? Notice;
    public VaultStore Store { get; }
    public Workspace? State { get; private set; }
    public bool IsUnlocked => State != null;
    public string SaveStatus { get; private set; } = "已保存";
    public string View { get; set; } = "home";
    public string SelectedId { get; set; } = "";
    public string Search { get; set; } = "";
    public string Sort { get; set; } = "updated";
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private bool dirty;
    private long epoch;
    public long WorkspaceEpoch=>epoch;
    internal Func<Task>? FlushEditor {get;set;}
    public WorkspaceViewModel(string? directory = null)
    { Store = new(directory ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MojiNotes")); saveTimer.Tick += async (_, _) => { saveTimer.Stop(); try { await SaveAsync(flushEditor:false); } catch (Exception) { Notice?.Invoke("暂时无法保存，可重试或导出存档。"); } }; }
    public async Task<bool> OpenAsync()
    {
        var state = await Store.ReadAsync();
        if (state == null && Store.HasLegacy) return false;
        if (state == null) { state = Seed.Create(); await Store.SaveAsync(state); }
        Open(state); if (Store.RecoveredBackup) Notice?.Invoke("已恢复上一份本地备份"); return true;
    }
    public async Task MigrateAsync(string password)
    {
        var envelope = await Store.ReadLegacyAsync();
        using var key = await Task.Run(() => CryptoService.Derive(password, envelope.Salt, envelope.Iterations));
        var state = await Task.Run(() => CryptoService.Decrypt<Workspace>(envelope, key)); state.Validate();
        await Store.SaveAsync(state); Open(state);
    }
    private void Open(Workspace state)
    { State = state; dirty = false; epoch++; View = "home"; SelectedId = ""; Search = ""; DataChanged?.Invoke("open"); }
    public void Change(Func<Workspace, Workspace> edit, string kind = "structure")
    { if (State == null) return; State = edit(State); dirty = true; SaveStatus = "保存中"; PropertyChanged?.Invoke(this, new(nameof(SaveStatus))); saveTimer.Stop(); saveTimer.Start(); DataChanged?.Invoke(kind); }
    public void EditNote(string id, Func<Note, Note> edit, bool checkpoint = false) => Change(v => v.ChangeNote(id, edit, checkpoint), "edit");
    public async Task SaveAsync(bool flushEditor = true)
    {
        Exception? flushFailure = null;
        try { if(flushEditor && FlushEditor!=null)await FlushEditor(); }
        catch(Exception error) { flushFailure=error;AppDiagnostics.Write("save-editor-flush",error.GetType().Name); }
        // Persist every acknowledged edit even if the renderer has stopped answering.
        await SaveReceivedAsync();
        if(flushFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(flushFailure).Throw();
    }
    private async Task SaveReceivedAsync()
    {
        saveTimer.Stop(); if (State == null || !dirty) return;
        await saveGate.WaitAsync();
        try { if (State == null) return; var state = State; await Task.Run(() => Store.SaveAsync(state)); if (ReferenceEquals(State, state)) dirty = false; SaveStatus = dirty ? "保存中" : "已保存"; PropertyChanged?.Invoke(this, new(nameof(SaveStatus))); if (dirty) saveTimer.Start(); }
        catch { SaveStatus = "保存失败"; PropertyChanged?.Invoke(this, new(nameof(SaveStatus))); throw; }
        finally { saveGate.Release(); }
    }
    public IEnumerable<Note> Filtered()
    {
        if (State == null) return [];
        var books = State.Notebooks.ToDictionary(b => b.Id); var notes = State.Notes.Where(n => n.PurgedAt == null && (View == "trash" ? n.DeletedAt != null : n.DeletedAt == null && books[n.NotebookId].DeletedAt == null && (View == "archive" ? n.Archived : !n.Archived)));
        if (View == "star") notes = notes.Where(n => n.Favorite); else if (State.LiveBooks.Any(b => b.Id == View)) notes = notes.Where(n => n.NotebookId == View);
        notes = notes.Where(n => RecordTools.Matches(n, Search, books[n.NotebookId].Mode));
        return Sort switch { "name" => notes.OrderByDescending(n => n.Pinned).ThenBy(n => n.Title, StringComparer.CurrentCulture), "created" => notes.OrderByDescending(n => n.Pinned).ThenByDescending(n => n.CreatedAt), _ => notes.OrderByDescending(n => n.Pinned).ThenByDescending(n => n.UpdatedAt) };
    }
    private SyncSettings CloudConfig()
    {
        if (State == null) throw new InvalidOperationException("本机数据尚未打开");
        CloudSaveService.ValidateConfig(State.Sync); return State.Sync;
    }
    public async Task<SaveCatalog> ListCloudSavesAsync()
    { var config = CloudConfig(); return await Task.Run(() => new CloudSaveService().ListAsync(config)); }
    public async Task<SaveCatalog> WriteCloudSaveAsync(SaveCatalog catalog, string name, SaveEntry? overwrite = null)
    { var config = CloudConfig(); await SaveAsync(); var state = State!; return await Task.Run(() => new CloudSaveService().WriteAsync(config, catalog, state, name, overwrite)); }
    public async Task<SaveCatalog> DeleteCloudSaveAsync(SaveCatalog catalog, SaveEntry entry)
    { var config = CloudConfig(); return await Task.Run(() => new CloudSaveService().DeleteAsync(config, catalog, entry)); }
    public async Task ReadCloudSaveAsync(SaveCatalog catalog, SaveEntry entry)
    {
        var config = CloudConfig(); var capturedEpoch = epoch;
        var payload = await Task.Run(() => new CloudSaveService().ReadAsync(config, catalog, entry));
        await ReplaceDataAsync(payload, capturedEpoch);
    }
    public async Task ReplaceDataAsync(SyncPayload payload, long? expectedEpoch = null)
    {
        if (State == null) throw new InvalidOperationException("本机数据尚未打开");
        await SaveAsync(); await saveGate.WaitAsync();
        try
        {
            if (State == null || (expectedEpoch != null && epoch != expectedEpoch)) throw new InvalidOperationException("空间已变化，请重新读取存档");
            var candidate = GameSaves.Apply(State, payload);
            await Task.Run(() => Store.SaveAsync(candidate));
            State = candidate; dirty = false; epoch++; View = "home"; SelectedId = ""; Search = ""; SaveStatus = "已保存";
        }
        finally { saveGate.Release(); }
        PropertyChanged?.Invoke(this, new(nameof(SaveStatus))); DataChanged?.Invoke("replace");
    }
    public void Checkpoint(string id) => Change(v => v with { Notes = v.Notes.Select(n => n.Id == id ? n with { History = new[] { n.Snapshot() }.Concat(n.History).Take(40).ToList(), UpdatedAt = Clock.Now, Revision = Clock.Id } : n).ToList() }, "edit");
    public void Dispose() { saveTimer.Stop(); State = null; }
}
