using System.IO;
using System.Text.Json;

namespace Moji.App;

// Local, bounded support log. Never includes document text, account data or tokens.
internal static class AppDiagnostics
{
    private static readonly object Gate = new();
    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MojiNotes", "diagnostics");
    internal static void Write(string operation, string detail)
    {
        var entry = JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, operation, detail });
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(DirectoryPath); var path = Path.Combine(DirectoryPath, "desktop.jsonl");
                    if (File.Exists(path) && new FileInfo(path).Length > 262144) File.Move(path, path + ".previous", true);
                    File.AppendAllText(path, entry + Environment.NewLine);
                }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        });
    }
}
