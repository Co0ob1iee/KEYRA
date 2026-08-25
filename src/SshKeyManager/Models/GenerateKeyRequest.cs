namespace SshKeyManager.Models;

public sealed class GenerateKeyRequest
{
    public required string Name { get; init; }

    public string Comment { get; init; } = string.Empty;

    public SshKeyAlgorithm Algorithm { get; init; } = SshKeyAlgorithm.Ed25519;

    public string? Passphrase { get; init; }
}
