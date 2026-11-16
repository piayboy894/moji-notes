using System.Text.Json;
using System.Text.Json.Serialization;

namespace Moji.Core;

[JsonConverter(typeof(JsonStringEnumConverter<NoteMode>))]
public enum NoteMode { Text, Prompt, Password }
public record Notebook
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "新记事本";
    public NoteMode Mode { get; init; }
    public string Icon { get; init; } = "";
    public string Color { get; init; } = "#73866A";
    public string ColorEnd { get; init; } = "";
    public long CreatedAt { get; init; } = Clock.Now;
    public long UpdatedAt { get; init; } = Clock.Now;
    public string Revision { get; init; } = Clock.Id;
    public long? DeletedAt { get; init; }
}
public record CredentialField
{
    public string Id { get; init; } = Clock.Id;
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
}
public record NoteVersion
{
    public string Id { get; init; } = Clock.Id;
    public long At { get; init; } = Clock.Now;
    public string Title { get; init; } = "";
    public string Content { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string Url { get; init; } = "";
    public string Otp { get; init; } = "";
    public List<string> Tags { get; init; } = []; // Legacy archive metadata; no tag UI or search.
    public List<PromptVariable> Variables { get; init; } = [];
    public List<CredentialField> Fields { get; init; } = [];
}
public record Note : NoteVersion
{
    public string NotebookId { get; init; } = "";
    public long CreatedAt { get; init; } = Clock.Now;
    public long UpdatedAt { get; init; } = Clock.Now;
    public string Revision { get; init; } = Clock.Id;
    public bool Favorite { get; init; }
    public bool Pinned { get; init; }
    public bool Archived { get; init; }
    public long? DeletedAt { get; init; }
    public long? PurgedAt { get; init; }
    public List<NoteVersion> History { get; init; } = [];
    public NoteVersion Snapshot() => new() { Title = Title, Content = Content, Username = Username, Password = Password, Url = Url, Otp = Otp, Tags = [.. Tags], Variables = [.. Variables], Fields = [.. Fields] };
    public Note Edited(bool checkpoint = false) => this with { Revision = Clock.Id, UpdatedAt = Clock.Now };
    public Note Apply(Note next, bool checkpoint = false)
    {
        var changed = Title != next.Title || Content != next.Content || Username != next.Username || Password != next.Password || Url != next.Url || Otp != next.Otp || !Variables.SequenceEqual(next.Variables) || !Fields.SequenceEqual(next.Fields);
        var history = changed && (checkpoint || History.Count == 0 || Clock.Now - History[0].At > 60_000) ? new[] { Snapshot() }.Concat(History).Take(40).ToList() : History;
        return next with { Revision = Clock.Id, UpdatedAt = Clock.Now, History = history };
    }
}
public record Preferences
{
    public string Theme { get; init; } = "light";
    public double FontSize { get; init; } = 15;
    public bool LineNumbers { get; init; }
    public bool WordWrap { get; init; } = true;
    public bool AutoSync { get; init; }
    public string EditorView { get; init; } = "split";
    public Dictionary<string, string> Shortcuts { get; init; } = [];
}
public record SyncSettings
{
    public string Repository { get; init; } = "";
    public string Code { get; init; } = "";
    public string Token { get; init; } = "";
    public long LastSync { get; init; }
    public Dictionary<string, string> Base { get; init; } = [];
}
public record Workspace
{
    public int Schema { get; init; } = 2;
    public string Id { get; init; } = Clock.Id;
    public List<Notebook> Notebooks { get; init; } = [];
    public List<Note> Notes { get; init; } = [];
    public Preferences Preferences { get; init; } = new();
    public SyncSettings Sync { get; init; } = new();
    [JsonIgnore] public IEnumerable<Notebook> LiveBooks => Notebooks.Where(b => b.DeletedAt == null);
    [JsonIgnore] public IEnumerable<Note> LiveNotes => Notes.Where(n => n.DeletedAt == null && n.PurgedAt == null && LiveBooks.Any(b => b.Id == n.NotebookId));
    public void Validate()
    {
        if (Schema is not (1 or 2) || Notebooks == null || Notes == null || Preferences == null || Sync == null || Notebooks.Count > 5000 || Notes.Count > 100000) throw new InvalidDataException("数据格式不受支持");
        if (Notebooks.Any(b => b == null || string.IsNullOrWhiteSpace(b.Id) || b.Name == null || b.Icon == null || b.Color == null || b.ColorEnd == null || !Enum.IsDefined(b.Mode) || b.Revision == null) || Notebooks.Select(b => b.Id).Distinct().Count() != Notebooks.Count) throw new InvalidDataException("记事本数据损坏");
        if (Notes.Any(n => n == null || string.IsNullOrWhiteSpace(n.Id) || string.IsNullOrWhiteSpace(n.Revision) || !Notebooks.Any(b => b.Id == n.NotebookId) || n.Title == null || n.Content == null || n.Password == null || n.Username == null || n.Url == null || n.Otp == null || n.Variables == null || n.Variables.Any(v => v == null || !System.Text.RegularExpressions.Regex.IsMatch(v.Id, "^[a-f0-9]{32}$") || string.IsNullOrWhiteSpace(v.Name) || v.Name.Length > 2000) || n.Variables.Select(v => v.Id).Distinct().Count() != n.Variables.Count || n.Tags == null || n.Tags.Any(t => t == null) || n.History == null || n.History.Any(h => h == null || h.Title == null || h.Content == null || h.Tags == null)) || Notes.Select(n => n.Id).Distinct().Count() != Notes.Count) throw new InvalidDataException("记录数据损坏");
        if(Notes.Any(n=>!CredentialFields.Valid(n.Fields)||n.History.Any(h=>!CredentialFields.Valid(h.Fields))))throw new InvalidDataException("自定义属性数据损坏");
        if (Preferences.FontSize is < 11 or > 32 || !new[] { "light", "dark", "system" }.Contains(Preferences.Theme) || Sync.Base == null) throw new InvalidDataException("设置数据损坏");
    }
    public Workspace ChangeNote(string id, Func<Note, Note> edit, bool checkpoint = false){var notes=Notes.Select(n=>n.Id==id&&n.PurgedAt==null?n.Apply(edit(n),checkpoint):n).ToList();return this with{Schema=notes.Any(n=>n.Fields.Count>0||n.History.Any(h=>h.Fields.Count>0))?2:Schema,Notes=notes};}
}
public record SyncPayload(int Schema, string Id, List<Notebook> Notebooks, List<Note> Notes);
public static class Clock { public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); public static string Id => Guid.NewGuid().ToString("N"); }
public static class Json
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = false };
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw new InvalidDataException("数据为空");
    public static Workspace Clone(Workspace workspace) => Deserialize<Workspace>(Serialize(workspace));
}
