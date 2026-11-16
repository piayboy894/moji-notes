using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Moji.Core;
public record UpdateAsset(long Id, string Name, string BrowserDownloadUrl, long Size);
public record ReleaseInfo(string TagName, bool Draft, bool Prerelease, string HtmlUrl, List<UpdateAsset> Assets);
public record UpdatePackage(Version Version, UpdateAsset Asset, UpdateAsset Checksums, string ReleaseUrl, string? Sha256 = null);
public sealed class UpdateService
{
    private const string Repository = "piayboy894/moji-notes";
    private readonly HttpClient http;
    public UpdateService(HttpClient? client = null) { http = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(15) }; http.DefaultRequestHeaders.UserAgent.ParseAdd("Moji-Notes/1.0"); }
    public Task<UpdatePackage?> CheckAsync(Version current, string arch, CancellationToken cancellation = default) => UpdateDiscovery.Check(http, current, arch, cancellation);
    private static Uri AssetUri(UpdateAsset asset)
    { var uri = new Uri(asset.BrowserDownloadUrl); if (uri.Scheme != "https" || uri.Host != "github.com" || !uri.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.Ordinal)) throw new InvalidDataException("更新来源不受信任"); return uri; }
    public async Task<string> DownloadAsync(UpdatePackage package, string directory, IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        var expected = package.Sha256;
        if (expected is null) {
            var checksumsText = await http.GetStringAsync(AssetUri(package.Checksums), cancellation); var checksums = Json.Deserialize<Dictionary<string,string>>(checksumsText.TrimStart('\uFEFF'));
            checksums.TryGetValue(package.Asset.Name, out expected);
        }
        if (expected is null || expected.Length != 64 || !expected.All(char.IsAsciiHexDigit)) throw new InvalidDataException("更新包缺少有效的校验值");
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "Moji.Notes.download.exe"); var temp = path + ".tmp";
        using var response = await http.GetAsync(AssetUri(package.Asset), HttpCompletionOption.ResponseHeadersRead, cancellation); response.EnsureSuccessStatusCode();
        await using (var input = await response.Content.ReadAsStreamAsync(cancellation))
        await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        { var buffer = new byte[81920]; long total = 0; int read; while ((read = await input.ReadAsync(buffer, cancellation)) > 0) { total += read; if (total > package.Asset.Size + 1024) throw new InvalidDataException("更新包大小异常"); await output.WriteAsync(buffer.AsMemory(0, read), cancellation); progress?.Report(package.Asset.Size == 0 ? 0 : (double)total / package.Asset.Size); } if (total != package.Asset.Size) throw new InvalidDataException("更新包下载不完整"); }
        await using var downloaded = File.OpenRead(temp); var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(downloaded, cancellation)); downloaded.Close();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expected.ToLowerInvariant()))) { File.Delete(temp); throw new InvalidDataException("更新包校验失败，已取消更新"); }
        File.Move(temp, path, true); return path;
    }
    public static void StartReplacement(string downloadedPath, string currentExecutable)
    {
        // Run the verified new executable as the updater. No PowerShell, scripts, or policy changes.
        var start = new ProcessStartInfo(downloadedPath) { UseShellExecute = false };
        start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(Environment.ProcessId.ToString()); start.ArgumentList.Add(currentExecutable); _ = Process.Start(start) ?? throw new IOException("无法启动更新程序");
    }
    public static async Task<int> ApplyReplacementAsync(string[] args)
    {
        if (args.Length < 3 || !int.TryParse(args[1], out var parent)) return 2;
        var target = Path.GetFullPath(args[2]); var source = Environment.ProcessPath!;
        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) return 2;
        try { using var p = Process.GetProcessById(parent); await p.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(1)); } catch (ArgumentException) { }
        for (var i = 0; i < 30; i++)
        {
            try { var temp = target + ".update"; File.Copy(source, temp, true); if (File.Exists(target)) File.Replace(temp, target, target + ".previous", true); else File.Move(temp, target); Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); return 0; }
            catch (IOException) { await Task.Delay(400); }
            catch (UnauthorizedAccessException) { return 3; }
        }
        return 4;
    }
}
