using SshKeyManager.Models;

namespace SshKeyManager.Services.Update;

public interface IAppUpdateService
{
    Version GetCurrentVersion();

    Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads setup.exe (preferred) or zip from the release and launches the installer / opens the folder.
    /// Returns true if the app should shut down (installer started).
    /// </summary>
    Task<bool> ApplyUpdateAsync(AppUpdateCheckResult update, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    void OpenReleasePage(AppUpdateCheckResult update);
}
