namespace SshKeyManager.Services.Security;

public interface IVaultSession
{
    bool IsUnlocked { get; }

    string Username { get; }

    /// <summary>In-memory Database Key (DBK). Formerly named master key in the JSON vault.</summary>
    byte[] MasterKey { get; }

    /// <summary>Alias for <see cref="MasterKey"/> (envelope DBK).</summary>
    byte[] DatabaseKey { get; }

    void Unlock(string username, byte[] databaseKey);

    void Lock();
}

public sealed class VaultSession : IVaultSession
{
    private SecureBuffer? _dbk;

    public bool IsUnlocked => _dbk is not null;

    public string Username { get; private set; } = string.Empty;

    public byte[] MasterKey => DatabaseKey;

    public byte[] DatabaseKey
    {
        get
        {
            if (_dbk is null)
            {
                throw new InvalidOperationException("Vault is locked.");
            }

            return _dbk.DangerousGetArray();
        }
    }

    public void Unlock(string username, byte[] databaseKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentNullException.ThrowIfNull(databaseKey);
        if (databaseKey.Length != CryptoUtilities.MasterKeySize)
        {
            throw new ArgumentException("Database key must be 256 bits.", nameof(databaseKey));
        }

        Lock();
        Username = username;
        _dbk = new SecureBuffer(databaseKey);
        SecureMemory.Memzero(databaseKey);
    }

    public void Lock()
    {
        _dbk?.Dispose();
        _dbk = null;
        Username = string.Empty;
    }
}
