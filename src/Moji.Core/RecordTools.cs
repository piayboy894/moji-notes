using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;

namespace Moji.Core;
public static partial class RecordTools
{
    public static int Strength(string password) => (password.Length >= 12 ? 1 : 0) + (password.Length >= 18 ? 1 : 0) + (password.Any(char.IsLower) && password.Any(char.IsUpper) ? 1 : 0) + (password.Any(char.IsDigit) && password.Any(c => !char.IsLetterOrDigit(c)) ? 1 : 0);
    public static bool Matches(Note note, string search, NoteMode mode)
    { if (string.IsNullOrWhiteSpace(search)) return true; return string.Join('\n', note.Title, note.Username, note.Url, string.Join("\n",note.Fields.Select(f=>f.Name+"\n"+f.Value)), mode == NoteMode.Password ? "" : Plain(PromptVariables.Display(note))).Contains(search, StringComparison.OrdinalIgnoreCase); }
    public static string Plain(string content) => Regex.Replace(EditorContent.Plain(content), @"\s+", " ").Trim();
    public static string ModeName(NoteMode mode) => mode switch { NoteMode.Text => "普通文本", NoteMode.Prompt => "提示词", _ => "账号密码" };
    public static string Relative(long time)
    { var diff = Clock.Now - time; return diff < 60000 ? "刚刚" : diff < 3600000 ? $"{diff / 60000} 分钟前" : diff < 86400000 ? $"{diff / 3600000} 小时前" : diff < 7 * 86400000 ? $"{diff / 86400000} 天前" : DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime.ToString("MM-dd"); }
    public static string SafeName(string name) { var safe = new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || "\\/:*?\"<>|".Contains(c) ? '_' : c).ToArray()); return string.IsNullOrWhiteSpace(safe) ? "未命名" : safe[..Math.Min(safe.Length, 100)]; }
    public static void ExportTexts(Workspace workspace, string path)
    { using var zip = ZipFile.Open(path, ZipArchiveMode.Create); foreach (var note in workspace.LiveNotes) { var book = workspace.Notebooks.First(b => b.Id == note.NotebookId); if (book.Mode == NoteMode.Password) continue; var entry = zip.CreateEntry($"{SafeName(book.Name)}/{SafeName(note.Title)}-{note.Id[..6]}.txt"); using var writer = new StreamWriter(entry.Open(), Encoding.UTF8); writer.WriteLine(note.Title+"\n\n"+PromptVariables.PlainText(note,note.Variables.ToDictionary(v=>v.Id,v=>v.Name),EditorContent.Plain)); } }
    public static string Totp(string secret, long? milliseconds = null)
    {
        var clean = Regex.Replace(secret.ToUpperInvariant(), @"[\s=-]", ""); if (!Regex.IsMatch(clean, "^[A-Z2-7]{16,128}$")) throw new InvalidDataException("动态验证码密钥格式不正确");
        var bits = string.Concat(clean.Select(c => Convert.ToString("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".IndexOf(c), 2).PadLeft(5, '0'))); var bytes = Enumerable.Range(0, bits.Length / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, 8), 2)).ToArray(); var counter = BitConverter.GetBytes((milliseconds ?? Clock.Now) / 30000); if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        try { var mac = HMACSHA1.HashData(bytes, counter); var offset = mac[^1] & 15; var number = ((mac[offset] & 127) << 24) | (mac[offset+1] << 16) | (mac[offset+2] << 8) | mac[offset+3]; return (number % 1_000_000).ToString("D6"); } finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
