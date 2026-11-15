using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Moji.Core;

public record SaveEntry(string Id, string Name, long SavedAt, int BookCount, int NoteCount, string BlobSha, string Platform, bool Legacy = false);
public record SaveIndex(string Format, int Version, List<SaveEntry> Entries);
public record SaveCatalog(string Source, string Branch, string? HeadSha, string? TreeSha, List<SaveEntry> Entries);
public static class GameSaves
{
    public static SyncPayload Capture(Workspace workspace)
    {
        workspace.Validate();
        return Json.Deserialize<SyncPayload>(Json.Serialize(new SyncPayload(workspace.Schema, workspace.Id, workspace.Notebooks, workspace.Notes)));
    }
    public static Workspace Apply(Workspace local, SyncPayload save)
    {
        var result = local with { Schema = save.Schema, Id = save.Id, Notebooks = save.Notebooks, Notes = save.Notes, Preferences = local.Preferences with { AutoSync = false } };
        result.Validate();
        return result;
    }
    public static void Validate(SaveIndex index)
    {
        if (index.Format != "moji-saves" || index.Version != 1 || index.Entries == null || index.Entries.Count > 500 || index.Entries.Any(e => e == null) || index.Entries.Select(e => e.Id).Distinct().Count() != index.Entries.Count)
            throw new InvalidDataException("云存档目录损坏或版本不受支持");
        foreach (var e in index.Entries)
            if (e.Id == null || e.BlobSha == null || !Regex.IsMatch(e.Id, "^[a-f0-9]{32}$") || string.IsNullOrWhiteSpace(e.Name) || e.Name.Length > 60 || e.SavedAt <= 0 || e.SavedAt > 253402300799999 || e.BookCount is < 0 or > 5000 || e.NoteCount is < 0 or > 100000 || !Regex.IsMatch(e.BlobSha, "^[a-f0-9]{40}$") || e.Legacy)
                throw new InvalidDataException("云存档信息损坏");
    }
}

// Each user action commits the encrypted catalog and save together. A stale branch
// head is rejected; no automatic merge, overwrite, retry, or choice of direction.
public sealed class CloudSaveService(HttpClient? client = null)
{
    private readonly HttpClient http = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    private static string Source(SyncSettings c) => CryptoService.Hash(c.Repository.ToLowerInvariant() + "\n" + c.Code);
    public static string Root(string code) => "moji-notes/" + CryptoService.Hash(code)[..40] + "/";
    public static string LegacyPath(string code) => "moji-notes/" + CryptoService.Hash(code)[..40] + ".moji";
    public static void ValidateConfig(SyncSettings c)
    {
        if (string.IsNullOrWhiteSpace(c.Token)) throw new InvalidOperationException("请先设置 GitHub Token");
        if (!Regex.IsMatch(c.Code, "^[A-Za-z0-9]{12,128}$")) throw new InvalidOperationException("存档码需为 12–128 位字母或数字");
        if (!Regex.IsMatch(c.Repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw new InvalidOperationException("仓库格式应为 用户名/仓库名");
    }
    private static void CheckSource(SyncSettings c, SaveCatalog catalog)
    {
        ValidateConfig(c);
        if (catalog.Source != Source(c)) throw new InvalidOperationException("连接已更改，请刷新存档列表");
    }
    private async Task<JsonElement?> Api(SyncSettings c, HttpMethod method, string path, object? body = null, bool missing = false, CancellationToken cancellation = default)
    {
        using var request = new HttpRequestMessage(method, "https://api.github.com/repos/" + c.Repository + path);
        request.Headers.UserAgent.ParseAdd("Moji-Notes/1.1"); request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", c.Token); request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (body != null) request.Content = new StringContent(Json.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, cancellation);
        if (missing && (response.StatusCode == HttpStatusCode.NotFound || (response.StatusCode == HttpStatusCode.Conflict && path.StartsWith("/git/ref/heads/")))) return null;
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "GitHub Token 已失效", HttpStatusCode.Forbidden => "仓库权限不足或请求受限，请检查 Contents 读写权限",
            HttpStatusCode.NotFound => "仓库或存档不存在，或当前 Token 无权访问",
            HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity => "云端已变化，请刷新列表后重新确认操作", _ => $"GitHub 请求失败（{(int)response.StatusCode}）"
        });
        var text = await response.Content.ReadAsStringAsync(cancellation);
        return string.IsNullOrWhiteSpace(text) ? JsonSerializer.SerializeToElement(new { }) : JsonSerializer.Deserialize<JsonElement>(text);
    }
    private async Task<string> DecodeFile(SyncSettings c, JsonElement file, CancellationToken cancellation)
    {
        if (file.TryGetProperty("size", out var size) && size.GetInt64() > 30_000_000) throw new InvalidDataException("存档超过容量限制");
        if (file.TryGetProperty("content", out var value) && !string.IsNullOrEmpty(value.GetString())) return Encoding.UTF8.GetString(Convert.FromBase64String(value.GetString()!));
        var blob = (await Api(c, HttpMethod.Get, "/git/blobs/" + file.GetProperty("sha").GetString(), cancellation: cancellation))!.Value;
        if (blob.GetProperty("size").GetInt64() > 30_000_000) throw new InvalidDataException("存档超过容量限制");
        return Encoding.UTF8.GetString(Convert.FromBase64String(blob.GetProperty("content").GetString()!));
    }
    private static T Decrypt<T>(string text, string code)
    {
        var envelope = Json.Deserialize<EncryptedEnvelope>(text); CryptoService.Validate(envelope);
        using var key = CryptoService.Derive(code, envelope.Salt, envelope.Iterations);
        return CryptoService.Decrypt<T>(envelope, key);
    }
    private static string Encrypt<T>(T value, string code)
    {
        using var key = CryptoService.NewKey(code); var text = Json.Serialize(CryptoService.Encrypt(value, key));
        if (text.Length > 25_000_000) throw new InvalidDataException("存档超过容量限制，请先导出较大的图片");
        return text;
    }
    public async Task<SaveCatalog> ListAsync(SyncSettings config, CancellationToken cancellation = default)
    {
        ValidateConfig(config);
        var repo = (await Api(config, HttpMethod.Get, "", cancellation: cancellation))!.Value;
        var branch = repo.TryGetProperty("default_branch", out var value) ? value.GetString() ?? "main" : "main";
        var head = await Api(config, HttpMethod.Get, "/git/ref/heads/" + Uri.EscapeDataString(branch), missing: true, cancellation: cancellation);
        if (head == null) return new(Source(config), branch, null, null, []);
        var headSha = head.Value.GetProperty("object").GetProperty("sha").GetString()!;
        var commit = (await Api(config, HttpMethod.Get, "/git/commits/" + headSha, cancellation: cancellation))!.Value;
        var tree = commit.GetProperty("tree").GetProperty("sha").GetString()!;
        var indexFile = await Api(config, HttpMethod.Get, "/contents/" + Root(config.Code) + "index.moji?ref=" + headSha, missing: true, cancellation: cancellation);
        var entries = new List<SaveEntry>();
        if (indexFile != null)
        {
            var index = Decrypt<SaveIndex>(await DecodeFile(config, indexFile.Value, cancellation), config.Code); GameSaves.Validate(index); entries.AddRange(index.Entries);
        }
        var legacy = await Api(config, HttpMethod.Get, "/contents/" + LegacyPath(config.Code) + "?ref=" + headSha, missing: true, cancellation: cancellation);
        if (legacy != null)
        {
            var payload = Decrypt<SyncPayload>(await DecodeFile(config, legacy.Value, cancellation), config.Code);
            var w = GameSaves.Apply(new(), payload);
            entries.Add(new("legacy", "旧版存档", 0, w.LiveBooks.Count(), w.LiveNotes.Count(), legacy.Value.GetProperty("sha").GetString()!, "旧版", true));
        }
        return new(Source(config), branch, headSha, tree, entries.OrderByDescending(e => e.SavedAt).ToList());
    }
    public async Task<SyncPayload> ReadAsync(SyncSettings config, SaveCatalog catalog, SaveEntry entry, CancellationToken cancellation = default)
    {
        CheckSource(config, catalog);
        if (!catalog.Entries.Contains(entry)) throw new InvalidOperationException("请重新选择存档");
        var blob = (await Api(config, HttpMethod.Get, "/git/blobs/" + entry.BlobSha, cancellation: cancellation))!.Value;
        var payload = Decrypt<SyncPayload>(await DecodeFile(config, blob, cancellation), config.Code);
        _ = GameSaves.Apply(new(), payload);
        return payload;
    }
    private async Task<string> Blob(SyncSettings config, string text, CancellationToken cancellation) =>
        (await Api(config, HttpMethod.Post, "/git/blobs", new { content = Convert.ToBase64String(Encoding.UTF8.GetBytes(text)), encoding = "base64" }, cancellation: cancellation))!.Value.GetProperty("sha").GetString()!;
    public async Task<SaveCatalog> WriteAsync(SyncSettings config, SaveCatalog catalog, Workspace workspace, string name, SaveEntry? overwrite = null, CancellationToken cancellation = default)
    {
        CheckSource(config, catalog); name = name.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 60) throw new InvalidOperationException("存档名称需为 1–60 个字符");
        if (overwrite != null && !catalog.Entries.Contains(overwrite)) throw new InvalidOperationException("请重新选择存档");
        if (overwrite == null && catalog.Entries.Count(e => !e.Legacy) >= 500) throw new InvalidOperationException("存档已达 500 份，请先删除不需要的存档");
        if (catalog.HeadSha == null)
        {
            var empty = Encrypt(new SaveIndex("moji-saves", 1, []), config.Code);
            await Api(config, HttpMethod.Put, "/contents/" + Root(config.Code) + "index.moji", new { message = "moji: initialize encrypted saves", content = Convert.ToBase64String(Encoding.UTF8.GetBytes(empty)) }, cancellation: cancellation);
            catalog = await ListAsync(config, cancellation);
        }
        var payload = GameSaves.Capture(workspace);
        var blobSha = await Blob(config, Encrypt(payload, config.Code), cancellation);
        var entry = new SaveEntry(overwrite?.Id ?? Clock.Id, overwrite?.Legacy == true ? "旧版存档" : name, Clock.Now, workspace.LiveBooks.Count(), workspace.LiveNotes.Count(), blobSha, "Windows", overwrite?.Legacy ?? false);
        var entries = catalog.Entries.Where(e => e.Id != entry.Id).Append(entry).OrderByDescending(e => e.SavedAt).ToList();
        return await Commit(config, catalog, entries, entry, false, cancellation);
    }
    public async Task<SaveCatalog> DeleteAsync(SyncSettings config, SaveCatalog catalog, SaveEntry entry, CancellationToken cancellation = default)
    {
        CheckSource(config, catalog);
        if (!catalog.Entries.Contains(entry)) throw new InvalidOperationException("请重新选择存档");
        return await Commit(config, catalog, catalog.Entries.Where(e => e.Id != entry.Id).ToList(), entry, true, cancellation);
    }
    private async Task<SaveCatalog> Commit(SyncSettings config, SaveCatalog catalog, List<SaveEntry> entries, SaveEntry changed, bool deleting, CancellationToken cancellation)
    {
        var index = new SaveIndex("moji-saves", 1, entries.Where(e => !e.Legacy).ToList()); GameSaves.Validate(index);
        var indexSha = await Blob(config, Encrypt(index, config.Code), cancellation);
        var changes = new object[] {
            new { path = Root(config.Code) + "index.moji", mode = "100644", type = "blob", sha = indexSha },
            new { path = changed.Legacy ? LegacyPath(config.Code) : Root(config.Code) + changed.Id + ".moji", mode = "100644", type = "blob", sha = deleting ? null : changed.BlobSha }
        };
        var treeBody = new Dictionary<string,object> { ["tree"] = changes }; if (catalog.TreeSha != null) treeBody["base_tree"] = catalog.TreeSha;
        var treeSha = (await Api(config, HttpMethod.Post, "/git/trees", treeBody, cancellation: cancellation))!.Value.GetProperty("sha").GetString()!;
        var commitSha = (await Api(config, HttpMethod.Post, "/git/commits", new { message = deleting ? "moji: delete encrypted save" : "moji: write encrypted save", tree = treeSha, parents = catalog.HeadSha == null ? Array.Empty<string>() : new[] { catalog.HeadSha } }, cancellation: cancellation))!.Value.GetProperty("sha").GetString()!;
        if (catalog.HeadSha == null) await Api(config, HttpMethod.Post, "/git/refs", new { @ref = "refs/heads/" + catalog.Branch, sha = commitSha }, cancellation: cancellation);
        else await Api(config, HttpMethod.Patch, "/git/refs/heads/" + Uri.EscapeDataString(catalog.Branch), new { sha = commitSha, force = false }, cancellation: cancellation);
        return catalog with { HeadSha = commitSha, TreeSha = treeSha, Entries = entries };
    }
}
