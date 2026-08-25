namespace SshKeyManager.Presentation;

public interface IShellLayoutService
{
    bool IsLogExpanded { get; set; }

    double LogPanelHeight { get; set; }

    double SidebarWidth { get; set; }

    double InspectorWidth { get; set; }

    Task PersistAsync(CancellationToken cancellationToken = default);
}
