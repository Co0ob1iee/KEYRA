using SshKeyManager.Models;

namespace SshKeyManager.Services;

public interface IAppSettingsService
{
    AppSettings Settings { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task SaveLanguageAsync(string cultureName, CancellationToken cancellationToken = default);
}
