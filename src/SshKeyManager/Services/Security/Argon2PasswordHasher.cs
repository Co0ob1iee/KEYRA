using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SshKeyManager.Services.Security;

public sealed class Argon2PasswordHasher
{
    private const int HashSize = 32;
    private const int Iterations = 4;
    private const int MemoryKb = 65536;
    private const int Parallelism = 4;

    public byte[] HashPassword(string password, byte[] salt)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentNullException.ThrowIfNull(salt);

        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            Iterations = Iterations,
            MemorySize = MemoryKb
        };

        return argon.GetBytes(HashSize);
    }

    public byte[] DeriveKek(string username, string password, byte[] salt)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentNullException.ThrowIfNull(salt);

        var input = $"{username}\0{password}";
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(input))
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            Iterations = Iterations,
            MemorySize = MemoryKb
        };

        return argon.GetBytes(CryptoUtilities.MasterKeySize);
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
}
