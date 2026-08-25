using SshKeyManager.Models;

namespace SshKeyManager.Services;

public interface IVaultStore
{
    string VaultDirectory { get; }

    string RootDirectory { get; }

    Task<IReadOnlyList<SshKeyRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<SshKeyRecord> SaveAsync(
        SshKeyRecord metadata,
        string privateKeyPem,
        CancellationToken cancellationToken = default);

    Task<SecureKeyMaterial> LoadPrivateKeyAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SshKeyRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
