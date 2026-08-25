namespace SshKeyManager.Services.Data;

public sealed class VaultMetadataRow
{
    public string Username { get; init; } = string.Empty;

    public byte[] Salt { get; init; } = [];

    public int ArgonMemory { get; init; }

    public int ArgonIterations { get; init; }

    public int ArgonParallelism { get; init; }

    public byte[] EncDbk { get; init; } = [];

    public byte[] DbkNonce { get; init; } = [];

    public byte[] DbkTag { get; init; } = [];

    public byte[]? IntegrityHmac { get; init; }

    public string CreatedAt { get; init; } = string.Empty;
}

public sealed class SshKeyRow
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string KeyType { get; init; } = string.Empty;

    public string PublicKey { get; init; } = string.Empty;

    public string FingerprintSha256 { get; init; } = string.Empty;

    public byte[] EncPrivateKey { get; init; } = [];

    public byte[] PrivateKeyNonce { get; init; } = [];

    public byte[] PrivateKeyTag { get; init; } = [];

    public byte[]? EncPassphrase { get; init; }

    public byte[]? PassphraseNonce { get; init; }

    public byte[]? PassphraseTag { get; init; }

    public string? Comment { get; init; }

    public string CreatedAt { get; init; } = string.Empty;

    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class ServerRow
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 22;

    public string Username { get; init; } = string.Empty;

    public string? DefaultKeyId { get; init; }

    public string? ProxyJumpId { get; init; }

    public string? Tags { get; init; }

    public string? Notes { get; init; }

    public string AuthMode { get; init; } = "key";

    public bool IsFavorite { get; init; }

    public string? LastConnectedAt { get; init; }

    public string CreatedAt { get; init; } = string.Empty;
}

public enum ConnectionLogStatus
{
    Success,
    Failed,
    Timeout
}

public static class ConnectionLogStatusNames
{
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
    public const string Timeout = "TIMEOUT";

    public static string ToDb(ConnectionLogStatus status) => status switch
    {
        ConnectionLogStatus.Success => Success,
        ConnectionLogStatus.Timeout => Timeout,
        _ => Failed
    };
}
