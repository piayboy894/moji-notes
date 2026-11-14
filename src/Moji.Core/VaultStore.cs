using System.Text;

namespace Moji.Core;
public sealed class VaultStore(string directory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public string DirectoryPath { get; } = directory;
    public string VaultPath => Path.Combine(DirectoryPath, "vault.moji");
    public bool RecoveredBackup { get; private set; }
    public async Task<EncryptedEnvelope?> ReadAsync()
    {
        RecoveredBackup = false;
        if (!File.Exists(VaultPath)) return null;
        try { return await ReadEnvelope(VaultPath); }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException or FormatException)
        { if (!File.Exists(VaultPath + ".bak")) throw; RecoveredBackup = true; return await ReadEnvelope(VaultPath + ".bak"); }
    }
    public static async Task<EncryptedEnvelope> ReadEnvelope(string path)
    { var info = new FileInfo(path); if (info.Length > 100_000_000) throw new InvalidDataException("加密文件过大"); var value = Json.Deserialize<EncryptedEnvelope>(await File.ReadAllTextAsync(path)); CryptoService.Validate(value); return value; }
    public async Task SaveAsync(Workspace workspace, VaultKey key, CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            Directory.CreateDirectory(DirectoryPath); var data = CryptoService.Encrypt(workspace, key); var temp = VaultPath + ".tmp";
            await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.WriteThrough)) { var bytes = Encoding.UTF8.GetBytes(Json.Serialize(data)); await stream.WriteAsync(bytes, cancellation); stream.Flush(true); }
            if (File.Exists(VaultPath)) File.Replace(temp, VaultPath, VaultPath + ".bak", true); else File.Move(temp, VaultPath);
        }
        finally { gate.Release(); }
    }
    public async Task ExportAsync(Workspace workspace, VaultKey key, string path) => await File.WriteAllTextAsync(path, Json.Serialize(CryptoService.Encrypt(workspace, key)));
}
