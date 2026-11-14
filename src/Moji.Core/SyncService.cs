using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace Moji.Core;
public record MergeResult(List<Notebook> Notebooks, List<Note> Notes, int Conflicts);
public static class WorkspaceMerge
{
    public static Dictionary<string, string> Revisions(Workspace workspace) => workspace.Notebooks.Select(b => (b.Id,b.Revision)).Concat(workspace.Notes.Select(n => (n.Id,n.Revision))).ToDictionary(n => n.Id, n => n.Revision);
    public static MergeResult Merge(Workspace local, SyncPayload remote, IReadOnlyDictionary<string,string> baseline)
    {
        var books = local.Notebooks.ToDictionary(b => b.Id);
        foreach (var b in remote.Notebooks) if (!books.TryGetValue(b.Id, out var previous) || b.UpdatedAt > previous.UpdatedAt) books[b.Id] = b;
        var notes = local.Notes.ToDictionary(n => n.Id); var conflicts = 0;
        foreach (var right in remote.Notes)
        {
            if (!notes.TryGetValue(right.Id, out var left)) { notes[right.Id] = right; continue; }
            if (left.Revision == right.Revision) continue;
            baseline.TryGetValue(right.Id, out var old); var lChanged = left.Revision != old; var rChanged = right.Revision != old;
            if (lChanged && rChanged)
            {
                var winner = left.PurgedAt != null ? left : right.PurgedAt != null ? right : left.DeletedAt != null ? left : right.DeletedAt != null ? right : left;
                var other = ReferenceEquals(winner, left) ? right : left; notes[right.Id] = winner;
                if (other.PurgedAt == null && other.DeletedAt == null)
                { var copy = other with { Id = Clock.Id, Title = other.Title + " · 同步副本", Revision = Clock.Id, UpdatedAt = Clock.Now, DeletedAt = null, PurgedAt = null }; notes[copy.Id] = copy; conflicts++; }
            }
            else if (rChanged) notes[right.Id] = right;
        }
        var removedBooks = books.Values.Where(b => b.DeletedAt != null).Select(b => b.Id).ToHashSet();
        foreach (var note in notes.Values.ToList()) if (removedBooks.Contains(note.NotebookId) && note.DeletedAt == null) notes[note.Id] = note with { DeletedAt = Clock.Now, UpdatedAt = Clock.Now, Revision = Clock.Id };
        return new(books.Values.ToList(), notes.Values.ToList(), conflicts);
    }
}
public sealed class SyncService(HttpClient? client = null)
{
    private readonly HttpClient http = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(50) };
    private record FileResponse(string Sha, string? Content, long Size);
    private HttpRequestMessage Request(HttpMethod method, string path, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, "https://api.github.com" + path); request.Headers.UserAgent.ParseAdd("Moji-Notes/1.0"); request.Headers.Accept.ParseAdd("application/vnd.github+json"); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); request.Headers.Add("X-GitHub-Api-Version", "2022-11-28"); if (body != null) request.Content = new StringContent(Json.Serialize(body), Encoding.UTF8, "application/json"); return request;
    }
    private static async Task Ensure(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        _ = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(response.StatusCode switch { HttpStatusCode.Unauthorized => "GitHub Token 已失效", HttpStatusCode.Forbidden => "仓库权限不足或请求受限，请检查 Contents 读写权限", HttpStatusCode.NotFound => "仓库不存在，或当前 Token 无权访问", HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity => "云端刚刚发生变化，请重新同步", _ => $"GitHub 请求失败（{(int)response.StatusCode}）" });
    }
    public static string DataPath(string code) => "moji-notes/" + CryptoService.Hash(code)[..40] + ".moji";
    public async Task<(Workspace Workspace, int Conflicts)> SynchronizeAsync(Workspace local, CancellationToken cancellation = default)
    {
        var config = local.Sync;
        if (string.IsNullOrWhiteSpace(config.Token)) throw new InvalidOperationException("请先填写 GitHub Token");
        if (!Regex.IsMatch(config.Code, "^[A-Za-z0-9]{12,128}$")) throw new InvalidOperationException("同步码需为 12–128 位字母或数字");
        if (!Regex.IsMatch(config.Repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw new InvalidOperationException("仓库格式应为 用户名/仓库名");
        var path = $"/repos/{config.Repository}/contents/{DataPath(config.Code)}"; string? sha = null; var result = new MergeResult(local.Notebooks, local.Notes, 0);
        using var get = Request(HttpMethod.Get, path, config.Token); using var response = await http.SendAsync(get, cancellation);
        if (response.IsSuccessStatusCode)
        {
            var file = Json.Deserialize<FileResponse>(await response.Content.ReadAsStringAsync(cancellation)); sha = file.Sha; if (file.Size > 30_000_000) throw new InvalidDataException("云端文件过大"); string content;
            if (!string.IsNullOrEmpty(file.Content)) content = Encoding.UTF8.GetString(Convert.FromBase64String(file.Content));
            else { using var blobRequest = Request(HttpMethod.Get, $"/repos/{config.Repository}/git/blobs/{sha}", config.Token); using var blobResponse = await http.SendAsync(blobRequest, cancellation); await Ensure(blobResponse); var blob = Json.Deserialize<FileResponse>(await blobResponse.Content.ReadAsStringAsync(cancellation)); content = Encoding.UTF8.GetString(Convert.FromBase64String(blob.Content!)); }
            var envelope = Json.Deserialize<EncryptedEnvelope>(content); using var key = CryptoService.Derive(config.Code, envelope.Salt, envelope.Iterations); var remote = CryptoService.Decrypt<SyncPayload>(envelope, key); (local with { Schema = remote.Schema, Notebooks = remote.Notebooks, Notes = remote.Notes }).Validate(); result = WorkspaceMerge.Merge(local, remote, config.Base);
        }
        else if (response.StatusCode == HttpStatusCode.NotFound)
        { using var repo = Request(HttpMethod.Get, $"/repos/{config.Repository}", config.Token); using var repoResponse = await http.SendAsync(repo, cancellation); await Ensure(repoResponse); }
        else await Ensure(response);
        var payload = new SyncPayload(1, local.Id, result.Notebooks, result.Notes); using var syncKey = CryptoService.NewKey(config.Code); var encrypted = CryptoService.Encrypt(payload, syncKey); var json = Json.Serialize(encrypted); if (json.Length > 25_000_000) throw new InvalidDataException("云同步文件超过容量，请导出较大的图片后重试");
        var body = new Dictionary<string, object> { ["message"] = "moji: sync encrypted workspace", ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)) }; if (sha != null) body["sha"] = sha;
        using var put = Request(HttpMethod.Put, path, config.Token, body); using var written = await http.SendAsync(put, cancellation); await Ensure(written);
        var merged = local with { Notebooks = result.Notebooks, Notes = result.Notes }; return (merged with { Sync = config with { LastSync = Clock.Now, Base = WorkspaceMerge.Revisions(merged) } }, result.Conflicts);
    }
}
