using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using SshKeyManager.Models;

namespace SshKeyManager.Services.Update;

public sealed class GitHubAppUpdateService : IAppUpdateService, IDisposable
{
    private readonly IAppSettingsService _settings;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public GitHubAppUpdateService(IAppSettingsService settings)
        : this(settings, CreateDefaultClient())
    {
        _ownsHttp = true;
    }

    internal GitHubAppUpdateService(IAppSettingsService settings, HttpClient http)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _ownsHttp = false;
    }

    public Version GetCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info) && Version.TryParse(StripPreRelease(info), out var fromInfo))
        {
            return Normalize(fromInfo);
        }

        var name = asm.GetName().Version;
        return name is null ? new Version(0, 0, 0) : Normalize(name);
    }

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var current = GetCurrentVersion();
        var owner = (_settings.Settings.UpdateGitHubOwner ?? string.Empty).Trim();
        var repo = (_settings.Settings.UpdateGitHubRepo ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
        {
            return new AppUpdateCheckResult
            {
                Status = AppUpdateStatus.NotConfigured,
                CurrentVersion = current,
                Message = "GitHub owner/repo not configured."
            };
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{owner}/{repo}/releases/latest");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new AppUpdateCheckResult
                {
                    Status = AppUpdateStatus.Failed,
                    CurrentVersion = current,
                    Message = "No published releases found for this repository."
                };
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? string.Empty : string.Empty;
            var htmlUrl = root.TryGetProperty("html_url", out var htmlEl) ? htmlEl.GetString() : null;
            var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            var draft = root.TryGetProperty("draft", out var draftEl) && draftEl.GetBoolean();
            var prerelease = root.TryGetProperty("prerelease", out var preEl) && preEl.GetBoolean();

            if (draft || (prerelease && !_settings.Settings.UpdateIncludePreReleases))
            {
                return new AppUpdateCheckResult
                {
                    Status = AppUpdateStatus.UpToDate,
                    CurrentVersion = current,
                    Message = "Latest GitHub release is draft/prerelease and was skipped."
                };
            }

            if (!TryParseTagVersion(tag, out var latest))
            {
                return new AppUpdateCheckResult
                {
                    Status = AppUpdateStatus.Failed,
                    CurrentVersion = current,
                    TagName = tag,
                    ReleaseHtmlUrl = htmlUrl,
                    Message = $"Could not parse version from tag '{tag}'."
                };
            }

            string? setupUrl = null;
            string? zipUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = asset.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }

                    if (assetName.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase)
                        || assetName.EndsWith("setup.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        setupUrl ??= url;
                    }
                    else if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                             && assetName.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
                    {
                        zipUrl ??= url;
                    }
                }
            }

            if (latest > current)
            {
                return new AppUpdateCheckResult
                {
                    Status = AppUpdateStatus.UpdateAvailable,
                    CurrentVersion = current,
                    LatestVersion = latest,
                    TagName = tag,
                    ReleaseHtmlUrl = htmlUrl,
                    ReleaseName = name,
                    SetupDownloadUrl = setupUrl,
                    ZipDownloadUrl = zipUrl
                };
            }

            return new AppUpdateCheckResult
            {
                Status = AppUpdateStatus.UpToDate,
                CurrentVersion = current,
                LatestVersion = latest,
                TagName = tag,
                ReleaseHtmlUrl = htmlUrl
            };
        }
        catch (Exception ex)
        {
            return new AppUpdateCheckResult
            {
                Status = AppUpdateStatus.Failed,
                CurrentVersion = current,
                Message = ex.Message
            };
        }
    }

    public async Task<bool> ApplyUpdateAsync(
        AppUpdateCheckResult update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var url = !string.IsNullOrWhiteSpace(update.SetupDownloadUrl)
            ? update.SetupDownloadUrl
            : update.ZipDownloadUrl;

        if (string.IsNullOrWhiteSpace(url))
        {
            OpenReleasePage(update);
            return false;
        }

        var isSetup = url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                      || (!string.IsNullOrWhiteSpace(update.SetupDownloadUrl)
                          && string.Equals(url, update.SetupDownloadUrl, StringComparison.Ordinal));

        var folder = Path.Combine(Path.GetTempPath(), "KEYRA-update");
        Directory.CreateDirectory(folder);
        var fileName = isSetup
            ? $"KEYRA-{update.LatestVersion?.ToString(3) ?? "update"}-setup.exe"
            : $"KEYRA-{update.LatestVersion?.ToString(3) ?? "update"}.zip";
        var targetPath = Path.Combine(folder, fileName);

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using (var remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var local = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await remote.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                       .ConfigureAwait(false)) > 0)
            {
                await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                readTotal += read;
                if (total is > 0)
                {
                    progress?.Report(Math.Clamp(readTotal / (double)total.Value, 0, 1));
                }
            }
        }

        progress?.Report(1);

        if (isSetup)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true
            });
            return true;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
        return false;
    }

    public void OpenReleasePage(AppUpdateCheckResult update)
    {
        var url = update.ReleaseHtmlUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            var owner = (_settings.Settings.UpdateGitHubOwner ?? string.Empty).Trim();
            var repo = (_settings.Settings.UpdateGitHubRepo ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo))
            {
                url = $"https://github.com/{owner}/{repo}/releases";
            }
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KEYRA-Updater");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static bool TryParseTagVersion(string tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var trimmed = tag.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        trimmed = StripPreRelease(trimmed);
        if (!Version.TryParse(trimmed, out var parsed))
        {
            return false;
        }

        version = Normalize(parsed);
        return true;
    }

    private static string StripPreRelease(string value)
    {
        var plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];
        }

        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            value = value[..dash];
        }

        return value.Trim();
    }

    private static Version Normalize(Version v) =>
        new(Math.Max(v.Major, 0), Math.Max(v.Minor, 0), Math.Max(v.Build < 0 ? 0 : v.Build, 0));
}
