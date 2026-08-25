namespace SshKeyManager.Services.Security;

public sealed class VaultSession : IVaultSession
{
    private byte[]? _masterKey;

    public bool IsUnlocked => _masterKey is not null;

    public string Username { get; private set; } = string.Empty;

    public byte[] MasterKey
    {
        get
        {
            if (_masterKey is null)
            {
                throw new InvalidOperationException("Vault is locked.");
            }

            return _masterKey;
        }
    }

    public void Unlock(string username, byte[] masterKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentNullException.ThrowIfNull(masterKey);
        if (masterKey.Length != CryptoUtilities.MasterKeySize)
        {
            throw new ArgumentException("Master key must be 256 bits.", nameof(masterKey));
        }

        Lock();
        Username = username;
        _masterKey = masterKey.ToArray();
    }

    public void Lock()
    {
        if (_masterKey is not null)
        {
            CryptoUtilities.CryptographicClear(_masterKey);
            _masterKey = null;
        }

        Username = string.Empty;
    }
}
