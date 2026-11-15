using System.Text;
namespace Moji.Core;
public sealed class VaultStore(string directory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public string DirectoryPath { get; } = directory;
    public string VaultPath => Path.Combine(DirectoryPath, "workspace.json");
    public string LegacyPath => Path.Combine(DirectoryPath, "vault.moji");
    public bool HasLegacy => File.Exists(LegacyPath);
    public bool RecoveredBackup { get; private set; }
    public async Task<Workspace?> ReadAsync()
    {
        RecoveredBackup = false;
        if (!File.Exists(VaultPath) && !File.Exists(VaultPath + ".bak")) return null;
        try { return await ReadWorkspace(VaultPath); }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException or FormatException)
        { if (!File.Exists(VaultPath + ".bak")) throw; RecoveredBackup = true; return await ReadWorkspace(VaultPath + ".bak"); }
    }
    private static async Task<Workspace> ReadWorkspace(string path)
    { if (new FileInfo(path).Length > 100_000_000) throw new InvalidDataException("本地文件过大"); var state = Json.Deserialize<Workspace>(await File.ReadAllTextAsync(path)); state.Validate(); return state; }
    public async Task<EncryptedEnvelope> ReadLegacyAsync()
    { try { return await ReadEnvelope(LegacyPath); } catch { if (!File.Exists(LegacyPath + ".bak")) throw; return await ReadEnvelope(LegacyPath + ".bak"); } }
    public static async Task<EncryptedEnvelope> ReadEnvelope(string path)
    { var info = new FileInfo(path); if (info.Length > 100_000_000) throw new InvalidDataException("加密文件过大"); var value = Json.Deserialize<EncryptedEnvelope>(await File.ReadAllTextAsync(path)); CryptoService.Validate(value); return value; }
    public async Task SaveAsync(Workspace workspace, CancellationToken cancellation = default)
    {
        workspace.Validate(); await gate.WaitAsync(cancellation);
        try
        {
            Directory.CreateDirectory(DirectoryPath); var temp = VaultPath + ".tmp";
            await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.WriteThrough)) { var bytes = Encoding.UTF8.GetBytes(Json.Serialize(workspace)); await stream.WriteAsync(bytes, cancellation); stream.Flush(true); }
            if (File.Exists(VaultPath)) File.Replace(temp, VaultPath, VaultPath + ".bak", true); else File.Move(temp, VaultPath);
        }
        finally { gate.Release(); }
    }
}
