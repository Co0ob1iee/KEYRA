using SshKeyManager.Models;
using SshKeyManager.Services.Data;
using SshKeyManager.Services.Security;

namespace SshKeyManager.Services;

public interface IConnectionAuditService
{
    Task LogAsync(
        Guid serverId,
        ConnectionLogStatus status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
}

public sealed class ConnectionAuditService : IConnectionAuditService
{
    private readonly KeyraRepository _repo;
    private readonly IVaultSession _session;

    public ConnectionAuditService(KeyraRepository repo, IVaultSession session)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public Task LogAsync(
        Guid serverId,
        ConnectionLogStatus status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (!_session.IsUnlocked || serverId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _repo.InsertConnectionLog(serverId.ToString("N"), status, errorMessage);
        return Task.CompletedTask;
    }
}

public sealed class SshConnectionProfileStore : ISshConnectionProfileStore
{
    private readonly KeyraRepository _repo;
    private readonly VaultPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _legacyImportAttempted;

    public SshConnectionProfileStore(KeyraRepository repo, VaultPaths paths)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task<IReadOnlyList<SshConnectionProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLegacyImportAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _repo.ListServers()
                .Select(ToProfile)
                .OrderByDescending(p => p.IsFavorite)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SshConnectionProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureLegacyImportAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var row = _repo.GetServer(id.ToString("N"));
            return row is null ? null : ToProfile(row);
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

        if (profile.ProxyJumpId == profile.Id && profile.Id != Guid.Empty)
        {
            throw new ArgumentException("A server cannot use itself as jump host.", nameof(profile));
        }

        await EnsureLegacyImportAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stored = Normalize(profile);
            if (stored.Id == Guid.Empty)
            {
                stored.Id = Guid.NewGuid();
            }

            _repo.UpsertServer(ToRow(stored));
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

        await EnsureLegacyImportAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _repo.DeleteServer(id.ToString("N"));
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLegacyImportAsync(CancellationToken cancellationToken)
    {
        if (_legacyImportAttempted)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_legacyImportAttempted)
            {
                return;
            }

            _legacyImportAttempted = true;
            if (_repo.ListServers().Count > 0 || !File.Exists(_paths.ConnectionsFilePath))
            {
                return;
            }

            try
            {
                await using var stream = File.OpenRead(_paths.ConnectionsFilePath);
                var document = await System.Text.Json.JsonSerializer
                    .DeserializeAsync<LegacyConnectionsDocument>(
                        stream,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                            Converters =
                            {
                                new System.Text.Json.Serialization.JsonStringEnumConverter(
                                    System.Text.Json.JsonNamingPolicy.CamelCase)
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                foreach (var profile in document?.Profiles ?? [])
                {
                    if (profile is null || profile.Id == Guid.Empty)
                    {
                        continue;
                    }

                    _repo.UpsertServer(ToRow(Normalize(profile)));
                }
            }
            catch
            {
                // Best-effort one-time import.
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static SshConnectionProfile ToProfile(ServerRow row)
    {
        _ = Guid.TryParse(row.Id, out var id);
        Guid? keyId = null;
        if (!string.IsNullOrWhiteSpace(row.DefaultKeyId) && Guid.TryParse(row.DefaultKeyId, out var parsedKey))
        {
            keyId = parsedKey;
        }

        Guid? jumpId = null;
        if (!string.IsNullOrWhiteSpace(row.ProxyJumpId) && Guid.TryParse(row.ProxyJumpId, out var parsedJump))
        {
            jumpId = parsedJump;
        }

        DateTime? last = null;
        if (!string.IsNullOrWhiteSpace(row.LastConnectedAt) &&
            DateTime.TryParse(row.LastConnectedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedLast))
        {
            last = parsedLast.ToUniversalTime();
        }

        return new SshConnectionProfile
        {
            Id = id,
            Name = row.Title,
            Host = row.Host,
            Port = row.Port,
            Username = row.Username,
            AuthMode = string.Equals(row.AuthMode, "password", StringComparison.OrdinalIgnoreCase)
                ? SshAuthMode.Password
                : SshAuthMode.Key,
            VaultKeyId = keyId,
            ProxyJumpId = jumpId,
            Tags = row.Tags,
            Notes = row.Notes,
            LastConnectedUtc = last,
            IsFavorite = row.IsFavorite
        };
    }

    private static ServerRow ToRow(SshConnectionProfile profile) => new()
    {
        Id = profile.Id.ToString("N"),
        Title = profile.Name,
        Host = profile.Host,
        Port = profile.Port,
        Username = profile.Username,
        DefaultKeyId = profile.VaultKeyId is { } key ? key.ToString("N") : null,
        ProxyJumpId = profile.ProxyJumpId is { } jump ? jump.ToString("N") : null,
        Tags = profile.Tags,
        Notes = profile.Notes,
        AuthMode = profile.AuthMode == SshAuthMode.Password ? "password" : "key",
        IsFavorite = profile.IsFavorite,
        LastConnectedAt = profile.LastConnectedUtc?.ToUniversalTime().ToString("O"),
        CreatedAt = DateTime.UtcNow.ToString("O")
    };

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

        if (clone.AuthMode == SshAuthMode.Password)
        {
            clone.VaultKeyId = null;
        }

        return clone;
    }

    private sealed class LegacyConnectionsDocument
    {
        public List<SshConnectionProfile?>? Profiles { get; set; }
    }
}
