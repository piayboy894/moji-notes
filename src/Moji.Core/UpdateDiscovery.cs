using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Moji.Core;

public sealed class UpdateCheckException(string message, Exception? inner = null) : Exception(message, inner);

internal static partial class UpdateDiscovery
{
    internal const string Repository = "piayboy894/moji-notes";
    private static readonly string[] Feeds = [
        $"https://raw.githubusercontent.com/{Repository}/main/updates/windows.json",
        $"https://github.com/{Repository}/releases/download/update-channel/windows.json"
    ];

    internal static async Task<UpdatePackage?> Check(HttpClient http, Version current, string arch, CancellationToken cancellation)
    {
        if (arch is not ("x64" or "arm64")) throw new ArgumentException("Unsupported architecture", nameof(arch));
        var failures = new List<Exception>();
        // Two small, public feeds use different GitHub delivery paths. Neither
        // sends sync credentials, depends on API quota, or downloads release history.
        var results = await Task.WhenAll(Feeds.Select(async url => {
            try { return (Package: ParseFeed(await Read(http, url, 65536, cancellation), arch), Error: (Exception?)null); }
            catch (Exception e) when (!cancellation.IsCancellationRequested) { return (Package: (UpdatePackage?)null, Error: e); }
        }));
        var valid = results.Where(r => r.Error is null).Select(r => r.Package!).OrderByDescending(p => p.Version).ToList();
        if (valid.Count > 0) return valid[0].Version > current ? valid[0] : null;
        failures.AddRange(results.Select(r => r.Error!));
        // Compatibility fallback for feeds that are unavailable or not yet
        // published. A check has bounded timeouts; package downloads keep theirs.
        try {
            var text = await Read(http, $"https://api.github.com/repos/{Repository}/releases?per_page=8", 1048576, cancellation);
            using var document = JsonDocument.Parse(text);
            var releases = document.RootElement.EnumerateArray()
                .Where(r => !r.GetProperty("draft").GetBoolean() && !r.GetProperty("prerelease").GetBoolean())
                .Select(r => (Release: r, Version: ParseVersion(r.GetProperty("tag_name").GetString()?.TrimStart('v'))))
                .Where(r => r.Version is not null && r.Version > current).OrderByDescending(r => r.Version);
            foreach (var (release, version) in releases) {
                var assets = release.GetProperty("assets").EnumerateArray().Select(a => new UpdateAsset(a.GetProperty("id").GetInt64(), a.GetProperty("name").GetString()!, a.GetProperty("browser_download_url").GetString()!, a.GetProperty("size").GetInt64())).ToList();
                var asset = assets.FirstOrDefault(a => a.Name == $"Moji-Notes-{version!.ToString(3)}-win-{arch}.exe");
                var hashes = assets.FirstOrDefault(a => a.Name == "SHA256SUMS.json");
                if (asset is null || hashes is null) continue;
                ValidateAsset(asset.BrowserDownloadUrl, $"v{version}", asset.Name); ValidateAsset(hashes.BrowserDownloadUrl, $"v{version}", hashes.Name);
                return new(version!, asset, hashes, ReleaseUrl(version!));
            }
            return null;
        } catch (Exception e) when (!cancellation.IsCancellationRequested) { failures.Add(e); }
        throw Failure(failures);
    }

    private static UpdatePackage ParseFeed(string text, string arch)
    {
        using var document = JsonDocument.Parse(text); var feed = document.RootElement;
        if (feed.GetProperty("schema").GetInt32() != 1 || feed.GetProperty("platform").GetString() != "windows") throw new InvalidDataException();
        var version = ParseVersion(feed.GetProperty("version").GetString()) ?? throw new InvalidDataException();
        if (feed.GetProperty("release_url").GetString() != ReleaseUrl(version)) throw new InvalidDataException();
        var name = $"Moji-Notes-{version.ToString(3)}-win-{arch}.exe";
        var entry = feed.GetProperty("assets").EnumerateArray().First(a => a.GetProperty("name").GetString() == name);
        var url = entry.GetProperty("url").GetString()!; ValidateAsset(url, $"v{version}", name);
        var size = entry.GetProperty("size").GetInt64(); var hash = entry.GetProperty("sha256").GetString()!;
        if (size <= 0 || !HashPattern().IsMatch(hash)) throw new InvalidDataException();
        return new(version, new(0, name, url, size), new(0, "SHA256SUMS.json", $"https://github.com/{Repository}/releases/download/v{version}/SHA256SUMS.json", 0), ReleaseUrl(version), hash.ToLowerInvariant());
    }

    private static Version? ParseVersion(string? value) => value is not null && VersionPattern().IsMatch(value) && Version.TryParse(value, out var version) ? version : null;
    private static string ReleaseUrl(Version version) => $"https://github.com/{Repository}/releases/tag/v{version.ToString(3)}";
    private static void ValidateAsset(string url, string tag, string name)
    { if (url != $"https://github.com/{Repository}/releases/download/{tag}/{name}") throw new InvalidDataException("更新来源不受信任"); }

    private static async Task<string> Read(HttpClient http, string url, int maximumBytes, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(8));
        for (var attempt = 0; ; attempt++) {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.CacheControl = new() { NoCache = true }; request.Headers.Accept.ParseAdd("application/json");
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (!response.IsSuccessStatusCode) {
                    if (attempt == 0 && ((int)response.StatusCode is 408 or 500 or 502 or 503 or 504)) { await Task.Delay(250, timeout.Token); continue; }
                    var limited = (int)response.StatusCode == 429 || (response.StatusCode == HttpStatusCode.Forbidden && response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) && values.Contains("0"));
                    throw new SourceError(response.StatusCode, limited);
                }
                if (response.Content.Headers.ContentLength > maximumBytes) throw new InvalidDataException();
                await using var input = await response.Content.ReadAsStreamAsync(timeout.Token); using var output = new MemoryStream();
                var buffer = new byte[8192]; int count;
                while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0) { if (output.Length + count > maximumBytes) throw new InvalidDataException(); output.Write(buffer, 0, count); }
                return Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF');
            } catch (HttpRequestException) when (attempt == 0 && !timeout.IsCancellationRequested) { await Task.Delay(250, timeout.Token); }
        }
    }

    private sealed class SourceError(HttpStatusCode status, bool limited) : Exception { internal HttpStatusCode Status { get; } = status; internal bool Limited { get; } = limited; }
    private static UpdateCheckException Failure(List<Exception> errors)
    {
        string message;
        if (errors.OfType<SourceError>().Any(e => e.Limited)) message = "GitHub 请求暂时受限，请稍后重试。";
        else if (errors.OfType<HttpRequestException>().Any(e => e.HttpRequestError == HttpRequestError.ProxyTunnelError)) message = "更新服务的代理连接失败，请检查系统代理设置。";
        else if (errors.OfType<HttpRequestException>().Any(e => e.HttpRequestError == HttpRequestError.SecureConnectionError)) message = "与更新服务器的安全连接失败，请检查系统时间或代理设置。";
        else if (errors.OfType<HttpRequestException>().Any(e => e.HttpRequestError == HttpRequestError.NameResolutionError)) message = "无法解析更新服务器地址，请检查 DNS 或代理设置。";
        else if (errors.Any(e => e is OperationCanceledException)) message = "更新服务响应超时，请稍后重试。";
        else if (errors.LastOrDefault() is SourceError source) message = $"更新服务暂时不可用（HTTP {(int)source.Status}），请稍后重试。";
        else if (errors.LastOrDefault() is JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException) message = "更新信息暂时无法读取，请稍后重试。";
        else message = "暂时无法连接更新服务器，可从 GitHub 下载新版。";
        return new(message, new AggregateException(errors));
    }
    [GeneratedRegex(@"^\d{1,3}\.\d{1,2}\.\d{1,2}$")] private static partial Regex VersionPattern();
    [GeneratedRegex(@"\A[a-fA-F0-9]{64}\z")] private static partial Regex HashPattern();
}
