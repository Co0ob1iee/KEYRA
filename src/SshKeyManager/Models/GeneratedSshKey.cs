namespace SshKeyManager.Models;

public sealed class GeneratedSshKey
{
    public required string PrivateKeyPem { get; init; }

    public required string PublicKey { get; init; }

    public required string Fingerprint { get; init; }

    public required SshKeyAlgorithm Algorithm { get; init; }

    public string Comment { get; init; } = string.Empty;

    public bool HasPassphrase { get; init; }
}
