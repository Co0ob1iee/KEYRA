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

    /// <summary>Optional bastion / jump host profile id (proxy_jump_id).</summary>
    public Guid? ProxyJumpId { get; set; }

    public string? Tags { get; set; }

    public string? Notes { get; set; }

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
        ProxyJumpId = ProxyJumpId,
        Tags = Tags,
        Notes = Notes,
        LastConnectedUtc = LastConnectedUtc,
        IsFavorite = IsFavorite
    };
}
