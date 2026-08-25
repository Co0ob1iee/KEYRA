using SshKeyManager.Models;
using SshKeyManager.Presentation;

namespace SshKeyManager.Services;

public sealed class ShellLayoutService : IShellLayoutService
{
    private readonly IAppSettingsService _settings;

    public ShellLayoutService(IAppSettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool IsLogExpanded
    {
        get => _settings.Settings.IsLogExpanded;
        set => _settings.Settings.IsLogExpanded = value;
    }

    public double LogPanelHeight
    {
        get => _settings.Settings.LogPanelHeight;
        set => _settings.Settings.LogPanelHeight = Clamp(value, 80, 240);
    }

    public double SidebarWidth
    {
        get => _settings.Settings.SidebarWidth;
        set => _settings.Settings.SidebarWidth = Clamp(value, 200, 400);
    }

    public double InspectorWidth
    {
        get => _settings.Settings.InspectorWidth;
        set => _settings.Settings.InspectorWidth = Clamp(value, 240, 480);
    }

    public async Task PersistAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _settings.SaveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Layout persistence is best-effort.
        }
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Clamp(double.IsNaN(value) || double.IsInfinity(value) ? min : value, min, max);
}
