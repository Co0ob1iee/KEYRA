namespace SshKeyManager.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "pl-PL";

    /// <summary>Persisted SSH terminal command history (most recent last).</summary>
    public List<string> SshCommandHistory { get; set; } = new();

    public int SshCommandHistoryMaxCount { get; set; } = 200;

    public bool IsLogExpanded { get; set; } = true;

    public double LogPanelHeight { get; set; } = 140;

    public double SidebarWidth { get; set; } = 280;

    public double InspectorWidth { get; set; } = 320;
}
