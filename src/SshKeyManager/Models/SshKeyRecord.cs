namespace SshKeyManager.Models;

public sealed class SshKeyRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public SshKeyAlgorithm Algorithm { get; set; }

    public string Comment { get; set; } = string.Empty;

    public string PublicKey { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public bool HasPassphrase { get; set; }
}
