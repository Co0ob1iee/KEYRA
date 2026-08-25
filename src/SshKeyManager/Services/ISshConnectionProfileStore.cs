using SshKeyManager.Models;

namespace SshKeyManager.Services;

public interface ISshConnectionProfileStore
{
    Task<IReadOnlyList<SshConnectionProfile>> ListAsync(CancellationToken cancellationToken = default);

    Task<SshConnectionProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SshConnectionProfile> UpsertAsync(SshConnectionProfile profile, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
