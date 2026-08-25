namespace SshKeyManager.Models;

public sealed class SshConnectionProfile
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 22;

    public string Username { get; set; } = string.Empty;

    public SshAuthMode AuthMode { get; set; } = SshAuthMode.Key;

    /// <summary>Vault key used when <see cref="AuthMode"/> is <see cref="SshAuthMode.Key"/>.</summary>
    public Guid? VaultKeyId { get; set; }

    public DateTime? LastConnectedUtc { get; set; }

    public bool IsFavorite { get; set; }

    public SshConnectionProfile Clone() => new()
    {
        Id = Id,
        Name = Name,
        Host = Host,
        Port = Port,
        Username = Username,
        AuthMode = AuthMode,
        VaultKeyId = VaultKeyId,
        LastConnectedUtc = LastConnectedUtc,
        IsFavorite = IsFavorite
    };
}
