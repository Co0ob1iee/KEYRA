namespace SshKeyManager.Models;

public enum AppUpdateStatus
{
    UpToDate,
    UpdateAvailable,
    NotConfigured,
    Failed
}

public sealed class AppUpdateCheckResult
{
    public AppUpdateStatus Status { get; init; }

    public string? Message { get; init; }

    public Version? CurrentVersion { get; init; }

    public Version? LatestVersion { get; init; }

    public string? TagName { get; init; }

    public string? ReleaseHtmlUrl { get; init; }

    public string? SetupDownloadUrl { get; init; }

    public string? ZipDownloadUrl { get; init; }

    public string? ReleaseName { get; init; }

    public bool HasDownloadableAsset =>
        !string.IsNullOrWhiteSpace(SetupDownloadUrl) || !string.IsNullOrWhiteSpace(ZipDownloadUrl);
}
