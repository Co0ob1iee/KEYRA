using System.Text.Json;
using SshKeyManager.Models;
using SshKeyManager.Services.Security;

namespace SshKeyManager.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly VaultPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AppSettingsService(VaultPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public AppSettings Settings { get; private set; } = new();

    private string SettingsFilePath => Path.Combine(_paths.RootDirectory, "settings.json");

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.RootDirectory);
            if (!File.Exists(SettingsFilePath))
            {
                Settings = new AppSettings();
                return;
            }

            await using var stream = File.OpenRead(SettingsFilePath);
            var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            Settings = loaded ?? new AppSettings();
        }
        catch (Exception)
        {
            Settings = new AppSettings();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.RootDirectory);
            await using var stream = File.Create(SettingsFilePath);
            await JsonSerializer.SerializeAsync(stream, Settings, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveLanguageAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        Settings.Language = cultureName;
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}
