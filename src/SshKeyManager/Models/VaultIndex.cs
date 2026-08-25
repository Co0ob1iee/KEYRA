namespace SshKeyManager.Models;

public sealed class VaultIndex
{
    public int Version { get; set; } = 1;

    public List<SshKeyRecord> Keys { get; set; } = new();
}
