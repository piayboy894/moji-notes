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
    public VaultKey? Key { get; private set; }
    public bool Demo { get; private set; }
    public bool IsUnlocked => State != null;
    public string SaveStatus { get; private set; } = "已加密保存";
    public string View { get; set; } = "home";
    public string SelectedId { get; set; } = "";
    public string Search { get; set; } = "";
    public string Sort { get; set; } = "updated";
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private bool dirty;
    private long epoch;
    public WorkspaceViewModel(string? directory = null)
    { Store = new(directory ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MojiNotes")); saveTimer.Tick += async (_, _) => { saveTimer.Stop(); try { await SaveAsync(); } catch (Exception e) { Notice?.Invoke("保存失败，请导出备份：" + e.Message); } }; }
    public async Task CreateAsync(string password)
    { if (password.Length < 10) throw new InvalidOperationException("主密码至少需要 10 个字符"); var key = await Task.Run(() => CryptoService.NewKey(password)); var state = Seed.Create(); await Task.Run(() => Store.SaveAsync(state, key)); Key?.Dispose(); Key = key; State = state; Demo = false; epoch++; View = "home"; DataChanged?.Invoke("unlock"); }
    public async Task UnlockAsync(string password, EncryptedEnvelope? backup = null)
    { var envelope = backup ?? await Store.ReadAsync() ?? throw new InvalidOperationException("找不到本地空间"); var key = await Task.Run(() => CryptoService.Derive(password, envelope.Salt, envelope.Iterations)); Workspace state; try { state = await Task.Run(() => CryptoService.Decrypt<Workspace>(envelope, key)); state.Validate(); if (backup != null) await Store.SaveAsync(state, key); } catch { key.Dispose(); throw; } Key?.Dispose(); Key = key; State = state; Demo = false; View = "home"; epoch++; DataChanged?.Invoke("unlock"); if (Store.RecoveredBackup) Notice?.Invoke("主文件无法读取，已恢复上一份加密备份"); }
    public void StartDemo() { State = Seed.Create(true); Demo = true; View = "home"; Key?.Dispose(); Key = null; epoch++; DataChanged?.Invoke("unlock"); }
    public void Change(Func<Workspace, Workspace> edit, string kind = "structure")
    { if (State == null) return; State = edit(State); dirty = true; SaveStatus = Demo ? "演示空间 · 不保存" : "保存中"; PropertyChanged?.Invoke(this, new(nameof(SaveStatus))); if (!Demo) { saveTimer.Stop(); saveTimer.Start(); } DataChanged?.Invoke(kind); }
    public void EditNote(string id, Func<Note, Note> edit, bool checkpoint = false) => Change(v => v.ChangeNote(id, edit, checkpoint), "edit");
    public async Task SaveAsync()
    {
        saveTimer.Stop(); if (State == null || Key == null || Demo || !dirty) return;
        await saveGate.WaitAsync();
        try { if (State == null || Key == null) return; var state = State; var key = Key; await Task.Run(() => Store.SaveAsync(state, key)); if (ReferenceEquals(State, state)) dirty = false; SaveStatus = dirty ? "保存中" : "已加密保存"; PropertyChanged?.Invoke(this, new(nameof(SaveStatus))); if (dirty) saveTimer.Start(); }
        catch { SaveStatus = "保存失败"; PropertyChanged?.Invoke(this, new(nameof(SaveStatus))); throw; }
        finally { saveGate.Release(); }
    }
    public async Task LockAsync()
    { await SaveAsync(); await saveGate.WaitAsync(); try { epoch++; Key?.Dispose(); Key = null; State = null; Search = ""; SelectedId = ""; dirty = false; Demo = false; DataChanged?.Invoke("lock"); } finally { saveGate.Release(); } }
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
        if (State == null || Key == null || Demo) throw new InvalidOperationException("请先创建或解锁本机空间");
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
        if (State == null || Key == null || Demo) throw new InvalidOperationException("请先创建或解锁本机空间");
        await SaveAsync(); await saveGate.WaitAsync();
        try
        {
            if (State == null || Key == null || (expectedEpoch != null && epoch != expectedEpoch)) throw new InvalidOperationException("空间已变化，请重新读取存档");
            var candidate = GameSaves.Apply(State, payload); var key = Key;
            await Task.Run(() => Store.SaveAsync(candidate, key));
            State = candidate; dirty = false; epoch++; View = "home"; SelectedId = ""; Search = ""; SaveStatus = "已加密保存";
        }
        finally { saveGate.Release(); }
        PropertyChanged?.Invoke(this, new(nameof(SaveStatus))); DataChanged?.Invoke("structure");
    }
    public async Task ChangePasswordAsync(string oldPassword, string nextPassword)
    { if (Demo || State == null || Key == null) throw new InvalidOperationException("演示空间不更换密码"); if (nextPassword.Length < 10) throw new InvalidOperationException("新主密码至少需要 10 个字符"); await SaveAsync(); var envelope = await Store.ReadAsync() ?? throw new InvalidDataException("本地文件不存在"); using var oldKey = await Task.Run(() => CryptoService.Derive(oldPassword, envelope.Salt, envelope.Iterations)); _ = CryptoService.Decrypt<Workspace>(envelope, oldKey); var next = await Task.Run(() => CryptoService.NewKey(nextPassword)); await saveGate.WaitAsync(); try { await Task.Run(() => Store.SaveAsync(State, next)); Key.Dispose(); Key = next; } catch { next.Dispose(); throw; } finally { saveGate.Release(); } }
    public void Checkpoint(string id) => Change(v => v with { Notes = v.Notes.Select(n => n.Id == id ? n with { History = new[] { n.Snapshot() }.Concat(n.History).Take(40).ToList(), UpdatedAt = Clock.Now, Revision = Clock.Id } : n).ToList() }, "edit");
    public void Dispose() { saveTimer.Stop(); Key?.Dispose(); Key = null; State = null; }
}
