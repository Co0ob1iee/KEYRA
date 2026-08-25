using System.Security.Cryptography;
using System.Text;

namespace SshKeyManager.Services.Security;

internal static class CryptoUtilities
{
    public const int MasterKeySize = 32;
    public const int SaltSize = 16;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int HmacSize = 32;

    public static byte[] GenerateRandomBytes(int length)
    {
        var buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    public static byte[] ComputeSha256(ReadOnlySpan<byte> data)
    {
        return SHA256.HashData(data);
    }

    public static byte[] ComputeSha256(string text)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(text));
    }

    public static byte[] ComputeHmacSha256(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        return HMACSHA256.HashData(key, data);
    }

    public static void CryptographicClear(byte[] data)
    {
        if (data.Length > 0)
        {
            Array.Clear(data, 0, data.Length);
        }
    }

    public static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return CryptographicOperations.FixedTimeEquals(left, right);
    }

    public static byte[] BuildKeyGaragePayload(string username, IEnumerable<KeyGarageEntry> entries)
    {
        using var ms = new MemoryStream();
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        ms.Write(usernameBytes, 0, usernameBytes.Length);

        foreach (var entry in entries.OrderBy(e => e.KeyId, StringComparer.Ordinal))
        {
            var idBytes = Encoding.UTF8.GetBytes(entry.KeyId);
            ms.WriteByte((byte)idBytes.Length);
            ms.Write(idBytes, 0, idBytes.Length);

            var fingerprintBytes = Encoding.UTF8.GetBytes(entry.Fingerprint);
            ms.WriteByte((byte)fingerprintBytes.Length);
            ms.Write(fingerprintBytes, 0, fingerprintBytes.Length);

            ms.Write(entry.PublicKeyHash, 0, entry.PublicKeyHash.Length);
        }

        return ms.ToArray();
    }
}

public readonly record struct KeyGarageEntry(string KeyId, string Fingerprint, byte[] PublicKeyHash);
