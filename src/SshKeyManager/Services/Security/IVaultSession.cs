namespace SshKeyManager.Services.Security;

public interface IVaultSession
{
    bool IsUnlocked { get; }

    string Username { get; }

    byte[] MasterKey { get; }

    void Unlock(string username, byte[] masterKey);

    void Lock();
}
