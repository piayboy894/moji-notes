using System.Security.Cryptography;
using System.Text;

namespace Moji.Core;
public record EncryptedEnvelope(string Format, int Version, string Salt, string Iv, string Data, int Iterations);
public sealed class VaultKey : IDisposable
{
    internal byte[] Bytes { get; }
    public string Salt { get; }
    public int Iterations { get; }
    internal VaultKey(byte[] bytes, string salt, int iterations) { Bytes = bytes; Salt = salt; Iterations = iterations; }
    public void Dispose() => CryptographicOperations.ZeroMemory(Bytes);
}
public static class CryptoService
{
    public const int Iterations = 310_000;
    public static VaultKey NewKey(string password) => Derive(password, Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)), Iterations);
    public static VaultKey Derive(string password, string salt, int iterations)
    {
        if (iterations is < 100_000 or > 2_000_000) throw new InvalidDataException("加密参数无效");
        var bytes = Convert.FromBase64String(salt); if (bytes.Length != 16) throw new InvalidDataException("加密文件已损坏");
        return new VaultKey(Rfc2898DeriveBytes.Pbkdf2(password, bytes, iterations, HashAlgorithmName.SHA256, 32), salt, iterations);
    }
    public static EncryptedEnvelope Encrypt<T>(T value, VaultKey key)
    {
        var plain = Encoding.UTF8.GetBytes(Json.Serialize(value)); var iv = RandomNumberGenerator.GetBytes(12); var cipher = new byte[plain.Length]; var tag = new byte[16];
        try { using var aes = new AesGcm(key.Bytes, 16); aes.Encrypt(iv, plain, cipher, tag); return new("moji-encrypted", 1, key.Salt, Convert.ToBase64String(iv), Convert.ToBase64String([.. cipher, .. tag]), key.Iterations); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static T Decrypt<T>(EncryptedEnvelope envelope, VaultKey key)
    {
        Validate(envelope); var data = Convert.FromBase64String(envelope.Data); var plain = new byte[data.Length - 16];
        try { using var aes = new AesGcm(key.Bytes, 16); aes.Decrypt(Convert.FromBase64String(envelope.Iv), data.AsSpan(0, data.Length - 16), data.AsSpan(data.Length - 16), plain); return Json.Deserialize<T>(Encoding.UTF8.GetString(plain)); }
        catch (CryptographicException) { throw new InvalidDataException("密码不正确，或加密文件已损坏"); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static void Validate(EncryptedEnvelope envelope)
    {
        if (envelope.Format != "moji-encrypted" || envelope.Version != 1 || Convert.FromBase64String(envelope.Iv).Length != 12 || Convert.FromBase64String(envelope.Salt).Length != 16 || Convert.FromBase64String(envelope.Data).Length < 16 || envelope.Iterations is < 100_000 or > 2_000_000) throw new InvalidDataException("这不是有效的墨记加密文件");
    }
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string GeneratePassword(int length = 20, bool symbols = true)
    {
        if (length is < 12 or > 128) throw new ArgumentOutOfRangeException(nameof(length));
        string[] groups = symbols ? ["ABCDEFGHJKLMNPQRSTUVWXYZ", "abcdefghijkmnopqrstuvwxyz", "23456789", "!@#$%&*+-=?"] : ["ABCDEFGHJKLMNPQRSTUVWXYZ", "abcdefghijkmnopqrstuvwxyz", "23456789"];
        var all = string.Concat(groups); var result = groups.Select(g => g[RandomNumberGenerator.GetInt32(g.Length)]).ToList();
        while (result.Count < length) result.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        for (var i = result.Count - 1; i > 0; i--) { var j = RandomNumberGenerator.GetInt32(i + 1); (result[i], result[j]) = (result[j], result[i]); }
        return new string(result.ToArray());
    }
}
