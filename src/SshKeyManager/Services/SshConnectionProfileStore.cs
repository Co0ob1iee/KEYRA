using System.Text.Json;
using System.Text.Json.Serialization;
using SshKeyManager.Models;
using SshKeyManager.Services.Security;

namespace SshKeyManager.Services;

public sealed class SshConnectionProfileStore : ISshConnectionProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly VaultPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<SshConnectionProfile> _cache = new();
    private bool _loaded;

    public SshConnectionProfileStore(VaultPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    private string ConnectionsFilePath => Path.Combine(_paths.RootDirectory, "connections.json");

    public async Task<IReadOnlyList<SshConnectionProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _cache
                .OrderByDescending(p => p.IsFavorite)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Clone())
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SshConnectionProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var match = _cache.FirstOrDefault(p => p.Id == id);
            return match?.Clone();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SshConnectionProfile> UpsertAsync(
        SshConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new ArgumentException("Profile name is required.", nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(profile.Host))
        {
            throw new ArgumentException("Host is required.", nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(profile.Username))
        {
            throw new ArgumentException("Username is required.", nameof(profile));
        }

        if (profile.Port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), profile.Port, "Port must be 1–65535.");
        }

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stored = profile.Clone();
            if (stored.Id == Guid.Empty)
            {
                stored.Id = Guid.NewGuid();
            }

            var index = _cache.FindIndex(p => p.Id == stored.Id);
            if (index >= 0)
            {
                _cache[index] = stored;
            }
            else
            {
                _cache.Add(stored);
            }

            await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
            return stored.Clone();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return false;
        }

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var removed = _cache.RemoveAll(p => p.Id == id) > 0;
            if (removed)
            {
                await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
            }

            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded)
            {
                return;
            }

            await LoadUnlockedAsync(cancellationToken).ConfigureAwait(false);
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LoadUnlockedAsync(CancellationToken cancellationToken)
    {
        try
        {
            _paths.EnsureDirectories();
            if (!File.Exists(ConnectionsFilePath))
            {
                _cache = new List<SshConnectionProfile>();
                return;
            }

            await using var stream = File.OpenRead(ConnectionsFilePath);
            var document = await JsonSerializer
                .DeserializeAsync<ConnectionsDocument>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            _cache = document?.Profiles?
                .Where(p => p is not null && p.Id != Guid.Empty)
                .Select(p => Normalize(p!))
                .ToList()
                ?? new List<SshConnectionProfile>();
        }
        catch (Exception)
        {
            _cache = new List<SshConnectionProfile>();
        }
    }

    private async Task SaveUnlockedAsync(CancellationToken cancellationToken)
    {
        _paths.EnsureDirectories();
        var document = new ConnectionsDocument
        {
            Profiles = _cache.Select(Normalize).ToList()
        };

        var tempPath = ConnectionsFilePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Copy(tempPath, ConnectionsFilePath, overwrite: true);
        File.Delete(tempPath);
    }

    private static SshConnectionProfile Normalize(SshConnectionProfile profile)
    {
        var clone = profile.Clone();
        clone.Name = (clone.Name ?? string.Empty).Trim();
        clone.Host = (clone.Host ?? string.Empty).Trim();
        clone.Username = (clone.Username ?? string.Empty).Trim();
        if (clone.Port is < 1 or > 65535)
        {
            clone.Port = 22;
        }

        // Passwords are never persisted on profiles.
        if (clone.AuthMode == SshAuthMode.Password)
        {
            clone.VaultKeyId = null;
        }

        return clone;
    }

    private sealed class ConnectionsDocument
    {
        public List<SshConnectionProfile> Profiles { get; set; } = new();
    }
}
