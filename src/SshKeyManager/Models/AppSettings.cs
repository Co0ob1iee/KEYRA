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

    /// <summary>GitHub user or org that hosts KEYRA releases (e.g. your username).</summary>
    public string UpdateGitHubOwner { get; set; } = string.Empty;

    /// <summary>GitHub repository name for releases.</summary>
    public string UpdateGitHubRepo { get; set; } = "KEYRA";

    /// <summary>When true, check GitHub Releases after the main shell loads.</summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    /// <summary>When true, treat GitHub prereleases as update candidates.</summary>
    public bool UpdateIncludePreReleases { get; set; }
}
