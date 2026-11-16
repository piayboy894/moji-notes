using System.Net;
using System.Security.Cryptography;
using Moji.Core;
using Xunit;

namespace Moji.Tests;

public class UpdateReliabilityTests
{
    private static string Feed(string version = "2.4.1", string? url = null) => Json.Serialize(new {
        schema = 1, platform = "windows", version, release_url = $"https://github.com/piayboy894/moji-notes/releases/tag/v{version}",
        assets = new[] { new { name = $"Moji-Notes-{version}-win-x64.exe", url = url ?? $"https://github.com/piayboy894/moji-notes/releases/download/v{version}/Moji-Notes-{version}-win-x64.exe", size = 4, sha256 = Convert.ToHexStringLower(SHA256.HashData("TEST"u8)) } }
    });
    private static HttpResponseMessage Reply(HttpStatusCode code, string text) => new(code) { Content = new StringContent(text) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> run) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => run(request, cancellationToken); }
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> run) => new(new Handler((r, _) => Task.FromResult(run(r))));

    [Fact] public async Task TinyFeedsWorkWithoutApiAccessAndChooseNewestValidVersion()
    {
        using var http = Client(r => {
            Assert.Null(r.Headers.Authorization); Assert.NotEmpty(r.Headers.UserAgent);
            Assert.NotEqual("api.github.com", r.RequestUri!.Host);
            return Reply(HttpStatusCode.OK, Feed(r.RequestUri.Host == "github.com" ? "2.4.1" : "2.4.0"));
        });
        var update = await new UpdateService(http).CheckAsync(new(2, 3, 0), "x64");
        Assert.Equal(new Version(2, 4, 1), update!.Version); Assert.NotNull(update.Sha256);
    }

    [Fact] public async Task FailedPrimaryUsesOtherFeedAndDoesNotCallApi()
    {
        using var http = Client(r => {
            Assert.NotEqual("api.github.com", r.RequestUri!.Host);
            if (r.RequestUri.Host.StartsWith("raw.")) throw new HttpRequestException("test transport failure");
            return Reply(HttpStatusCode.OK, Feed());
        });
        Assert.Equal(new Version(2, 4, 1), (await new UpdateService(http).CheckAsync(new(2, 3, 0), "x64"))!.Version);
    }

    [Fact] public async Task CurrentFeedReturnsNoUpdateWithoutApiRequest()
    {
        using var http = Client(r => { Assert.NotEqual("api.github.com", r.RequestUri!.Host); return Reply(HttpStatusCode.OK, Feed()); });
        Assert.Null(await new UpdateService(http).CheckAsync(new(2, 4, 1), "x64"));
    }

    [Fact] public async Task ApiFallbackRetriesTransientFailureAndSelectsLatestWindowsRelease()
    {
        var attempts = 0;
        using var http = Client(r => {
            if (r.RequestUri!.Host != "api.github.com") return Reply(HttpStatusCode.NotFound, "missing");
            if (++attempts == 1) return Reply(HttpStatusCode.ServiceUnavailable, "temporary");
            object Release(string tag, bool preview = false) => new { tag_name = tag, draft = false, prerelease = preview, assets = new[] {
                new { id = 1, name = $"Moji-Notes-{tag.TrimStart('v')}-win-x64.exe", browser_download_url = $"https://github.com/piayboy894/moji-notes/releases/download/{tag}/Moji-Notes-{tag.TrimStart('v')}-win-x64.exe", size = 4 },
                new { id = 2, name = "SHA256SUMS.json", browser_download_url = $"https://github.com/piayboy894/moji-notes/releases/download/{tag}/SHA256SUMS.json", size = 80 }
            } };
            return Reply(HttpStatusCode.OK, Json.Serialize(new[] { Release("v2.0.0"), Release("android-v9.0.0"), Release("v2.4.1"), Release("v9.0.0", true) }));
        });
        Assert.Equal(new Version(2, 4, 1), (await new UpdateService(http).CheckAsync(new(1, 0, 0), "x64"))!.Version); Assert.Equal(2, attempts);
    }

    [Fact] public async Task RateLimitIsNotBlamedOnUserNetworkAndNeverMeansUpToDate()
    {
        using var http = Client(_ => { var r = Reply(HttpStatusCode.Forbidden, "limited"); r.Headers.Add("X-RateLimit-Remaining", "0"); return r; });
        var error = await Assert.ThrowsAsync<UpdateCheckException>(() => new UpdateService(http).CheckAsync(new(2, 3, 0), "x64"));
        Assert.Contains("受限", UserMessages.For(error)); Assert.DoesNotContain("检查网络", UserMessages.For(error));
    }

    [Fact] public async Task UntrustedFeedAndMalformedFallbackFailInsteadOfClaimingCurrent()
    {
        using var http = Client(r => Reply(HttpStatusCode.OK, r.RequestUri!.Host == "api.github.com" ? "not json" : Feed(url: "https://untrusted.invalid/app.exe")));
        await Assert.ThrowsAsync<UpdateCheckException>(() => new UpdateService(http).CheckAsync(new(2, 3, 0), "x64"));
    }

    [Fact] public async Task CancellationStopsCheckWithoutApiFallback()
    {
        using var cancellation = new CancellationTokenSource(); var calls = 0;
        using var http = new HttpClient(new Handler(async (r, token) => {
            Interlocked.Increment(ref calls); Assert.NotEqual("api.github.com", r.RequestUri!.Host);
            cancellation.Cancel(); await Task.Delay(100, token); return Reply(HttpStatusCode.OK, Feed());
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UpdateService(http).CheckAsync(new(2, 3, 0), "x64", cancellation.Token)); Assert.InRange(calls, 1, 2);
    }

    [Fact] public async Task FeedHashVerifiesDownloadedBytesWithoutFetchingChecksumFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "moji-update-" + Clock.Id);
        using var http = Client(r => Reply(HttpStatusCode.OK, r.RequestUri!.AbsolutePath.EndsWith(".exe") ? "TEST" : Feed()));
        try {
            var updater = new UpdateService(http); var package = (await updater.CheckAsync(new(2, 3, 0), "x64"))!;
            var path = await updater.DownloadAsync(package, directory); Assert.Equal("TEST", await File.ReadAllTextAsync(path));
            await Assert.ThrowsAsync<InvalidDataException>(() => updater.DownloadAsync(package with { Sha256 = new string('0', 64) }, directory));
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
