using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SshKeyManager.Services.Security;

/// <summary>
/// Argon2id KDF. Default params match existing KEYRA vaults (memory 65536 KiB, iterations 4, parallelism 4).
/// Spec examples often cite iterations=3; we keep 4 for continuity and store params in vault_metadata.
/// </summary>
public sealed class Argon2PasswordHasher
{
    public const int DefaultHashSize = 32;
    public const int DefaultIterations = 4;
    public const int DefaultMemoryKb = 65536;
    public const int DefaultParallelism = 4;

    public byte[] HashPassword(string password, byte[] salt)
    {
        return Derive(
            Encoding.UTF8.GetBytes(password),
            salt,
            DefaultMemoryKb,
            DefaultIterations,
            DefaultParallelism,
            DefaultHashSize);
    }

    public byte[] DeriveKek(string username, string password, byte[] salt)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentNullException.ThrowIfNull(salt);

        var input = $"{username}\0{password}";
        return Derive(
            Encoding.UTF8.GetBytes(input),
            salt,
            DefaultMemoryKb,
            DefaultIterations,
            DefaultParallelism,
            CryptoUtilities.MasterKeySize);
    }

    /// <summary>Derive MEK from master password + salt (envelope encryption).</summary>
    public byte[] DeriveMek(
        string password,
        byte[] salt,
        int memoryKb,
        int iterations,
        int parallelism)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentNullException.ThrowIfNull(salt);

        return Derive(
            Encoding.UTF8.GetBytes(password),
            salt,
            memoryKb,
            iterations,
            parallelism,
            CryptoUtilities.MasterKeySize);
    }

    public bool VerifyPassword(string password, byte[] salt, byte[] expectedHash)
    {
        var computed = HashPassword(password, salt);
        try
        {
            return CryptoUtilities.FixedTimeEquals(computed, expectedHash);
        }
        finally
        {
            CryptoUtilities.CryptographicClear(computed);
        }
    }

    private static byte[] Derive(
        byte[] passwordBytes,
        byte[] salt,
        int memoryKb,
        int iterations,
        int parallelism,
        int hashSize)
    {
        try
        {
            using var argon = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = parallelism,
                Iterations = iterations,
                MemorySize = memoryKb
            };

            return argon.GetBytes(hashSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
}
