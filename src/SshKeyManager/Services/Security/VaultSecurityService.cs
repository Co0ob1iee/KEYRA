using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SshKeyManager.Models;
using SshKeyManager.Services.Data;

namespace SshKeyManager.Services.Security;

public interface IVaultSecurityService
{
    bool IsSetupComplete { get; }

    bool NeedsLegacyMigration { get; }

    string RootDirectory { get; }

    string DatabasePath { get; }

    Task CompleteSetupAsync(string username, string password, CancellationToken cancellationToken = default);

    Task UnlockAsync(string username, string password, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    void Lock();
}

public sealed class VaultSecurityService : IVaultSecurityService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly VaultPaths _paths;
    private readonly KeyraDb _db;
    private readonly KeyraRepository _repo;
    private readonly Argon2PasswordHasher _hasher;
    private readonly AesGcmVaultCrypto _crypto;
    private readonly KeyGarageHashService _keyGarage;
    private readonly IVaultSession _session;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public VaultSecurityService(
        VaultPaths paths,
        KeyraDb db,
        KeyraRepository repo,
        Argon2PasswordHasher hasher,
        AesGcmVaultCrypto crypto,
        KeyGarageHashService keyGarage,
        IVaultSession session)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
        _keyGarage = keyGarage ?? throw new ArgumentNullException(nameof(keyGarage));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public bool IsSetupComplete =>
        _db.HasVaultMetadata() || (_paths.HasLegacyVault && !_db.HasVaultMetadata());

    public bool NeedsLegacyMigration => _paths.HasLegacyVault && !_db.HasVaultMetadata();

    public string RootDirectory => _paths.RootDirectory;

    public string DatabasePath => _paths.DatabasePath;

    public async Task CompleteSetupAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrEmpty(password);

        username = username.Trim();
        if (username.Length < 2)
        {
            throw new ArgumentException("Username must be at least 2 characters.", nameof(username));
        }

        if (password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters.", nameof(password));
        }

        if (_db.HasVaultMetadata())
        {
            throw new InvalidOperationException("Vault setup is already complete.");
        }

        if (_paths.HasLegacyVault)
        {
            throw new InvalidOperationException(
                "A legacy JSON vault was found. Unlock with your existing credentials to migrate, or remove the old vault folder for a fresh setup.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _paths.EnsureDirectories();
            _db.EnsureCreated();

            var salt = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.SaltSize);
            var dbk = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.MasterKeySize);
            SecureBuffer? mekBuffer = null;
            try
            {
                var mek = _hasher.DeriveMek(
                    password,
                    salt,
                    Argon2PasswordHasher.DefaultMemoryKb,
                    Argon2PasswordHasher.DefaultIterations,
                    Argon2PasswordHasher.DefaultParallelism);
                mekBuffer = new SecureBuffer(mek);
                SecureMemory.Memzero(mek);

                var enc = _crypto.EncryptParts(mekBuffer.Span, dbk);
                _repo.UpsertVaultMetadata(new VaultMetadataRow
                {
                    Username = username,
                    Salt = salt,
                    ArgonMemory = Argon2PasswordHasher.DefaultMemoryKb,
                    ArgonIterations = Argon2PasswordHasher.DefaultIterations,
                    ArgonParallelism = Argon2PasswordHasher.DefaultParallelism,
                    EncDbk = enc.Ciphertext,
                    DbkNonce = enc.Nonce,
                    DbkTag = enc.Tag,
                    CreatedAt = DateTime.UtcNow.ToString("O")
                });

                await _keyGarage.WriteAsync(dbk, username, Array.Empty<SshKeyRecord>(), cancellationToken)
                    .ConfigureAwait(false);
                UpdateIntegrityHmac(dbk, username, Array.Empty<SshKeyRecord>());
                _session.Unlock(username, dbk);
            }
            finally
            {
                mekBuffer?.Dispose();
                SecureMemory.Memzero(dbk);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ArgumentException and not InvalidOperationException)
        {
            throw new InvalidOperationException("Vault setup failed.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UnlockAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrEmpty(password);
        username = username.Trim();

        if (!IsSetupComplete)
        {
            throw new InvalidOperationException("Vault is not configured.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (NeedsLegacyMigration)
            {
                await MigrateLegacyVaultAsync(username, password, cancellationToken).ConfigureAwait(false);
                return;
            }

            _db.EnsureCreated();
            var meta = _repo.GetVaultMetadata()
                ?? throw new InvalidOperationException("Vault metadata is missing.");

            if (!string.Equals(meta.Username, username, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Invalid username or password.");
            }

            SecureBuffer? mekBuffer = null;
            byte[]? dbk = null;
            try
            {
                var mek = _hasher.DeriveMek(
                    password,
                    meta.Salt,
                    meta.ArgonMemory,
                    meta.ArgonIterations,
                    meta.ArgonParallelism);
                mekBuffer = new SecureBuffer(mek);
                SecureMemory.Memzero(mek);

                try
                {
                    dbk = _crypto.DecryptParts(mekBuffer.Span, meta.EncDbk, meta.DbkNonce, meta.DbkTag);
                }
                catch (CryptographicException)
                {
                    throw new UnauthorizedAccessException("Invalid username or password.");
                }

                var keys = MapKeys(_repo.ListKeys());
                await _keyGarage.VerifyAsync(dbk, username, keys, cancellationToken).ConfigureAwait(false);
                VerifyIntegrityHmac(meta, dbk, username, keys);
                _session.Unlock(username, dbk);
                dbk = null;
            }
            finally
            {
                mekBuffer?.Dispose();
                if (dbk is not null)
                {
                    SecureMemory.Memzero(dbk);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not InvalidOperationException)
        {
            throw new InvalidOperationException("Vault unlock failed.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(currentPassword);
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        if (newPassword.Length < 8)
        {
            throw new ArgumentException("New password must be at least 8 characters.", nameof(newPassword));
        }

        if (!_session.IsUnlocked)
        {
            throw new InvalidOperationException("Vault must be unlocked to change password.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var username = _session.Username;
            var meta = _repo.GetVaultMetadata()
                ?? throw new InvalidOperationException("Vault metadata is missing.");

            if (!string.Equals(meta.Username, username, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Session username does not match vault metadata.");
            }

            SecureBuffer? currentMek = null;
            SecureBuffer? newMek = null;
            try
            {
                var mekBytes = _hasher.DeriveMek(
                    currentPassword,
                    meta.Salt,
                    meta.ArgonMemory,
                    meta.ArgonIterations,
                    meta.ArgonParallelism);
                currentMek = new SecureBuffer(mekBytes);
                SecureMemory.Memzero(mekBytes);

                try
                {
                    _ = _crypto.DecryptParts(currentMek.Span, meta.EncDbk, meta.DbkNonce, meta.DbkTag);
                }
                catch (CryptographicException)
                {
                    throw new UnauthorizedAccessException("Current password is incorrect.");
                }

                var newSalt = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.SaltSize);
                var newMekBytes = _hasher.DeriveMek(
                    newPassword,
                    newSalt,
                    Argon2PasswordHasher.DefaultMemoryKb,
                    Argon2PasswordHasher.DefaultIterations,
                    Argon2PasswordHasher.DefaultParallelism);
                newMek = new SecureBuffer(newMekBytes);
                SecureMemory.Memzero(newMekBytes);

                var dbk = _session.DatabaseKey;
                var enc = _crypto.EncryptParts(newMek.Span, dbk);
                var keys = MapKeys(_repo.ListKeys());
                _repo.UpsertVaultMetadata(new VaultMetadataRow
                {
                    Username = username,
                    Salt = newSalt,
                    ArgonMemory = Argon2PasswordHasher.DefaultMemoryKb,
                    ArgonIterations = Argon2PasswordHasher.DefaultIterations,
                    ArgonParallelism = Argon2PasswordHasher.DefaultParallelism,
                    EncDbk = enc.Ciphertext,
                    DbkNonce = enc.Nonce,
                    DbkTag = enc.Tag,
                    CreatedAt = meta.CreatedAt
                });

                await _keyGarage.WriteAsync(dbk, username, keys, cancellationToken).ConfigureAwait(false);
                UpdateIntegrityHmac(dbk, username, keys);
            }
            finally
            {
                currentMek?.Dispose();
                newMek?.Dispose();
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ArgumentException)
        {
            throw new InvalidOperationException("Password change failed.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Lock()
    {
        _session.Lock();
    }

    private async Task MigrateLegacyVaultAsync(string username, string password, CancellationToken cancellationToken)
    {
        var fileBytes = await File.ReadAllBytesAsync(_paths.MasterKeyFilePath, cancellationToken).ConfigureAwait(false);
        var fileData = MasterKeyFileFormat.Deserialize(fileBytes);

        if (!string.Equals(fileData.Username, username, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (!_hasher.VerifyPassword(password, fileData.PasswordVerifierSalt, fileData.PasswordVerifierHash))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        byte[]? oldMaster = null;
        var kek = _hasher.DeriveKek(username, password, fileData.KekSalt);
        try
        {
            try
            {
                oldMaster = _crypto.DecryptMasterKey(kek, fileData.EncryptedMasterKey);
            }
            catch (CryptographicException)
            {
                throw new UnauthorizedAccessException("Invalid username or password.");
            }
        }
        finally
        {
            CryptoUtilities.CryptographicClear(kek);
        }

        try
        {
            var legacyKeys = await ReadLegacyIndexAsync(cancellationToken).ConfigureAwait(false);
            if (File.Exists(_paths.KeyGarageHashPath))
            {
                await _keyGarage.VerifyAsync(oldMaster!, username, legacyKeys, cancellationToken).ConfigureAwait(false);
            }

            _db.EnsureCreated();
            var salt = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.SaltSize);
            var dbk = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.MasterKeySize);
            SecureBuffer? mekBuffer = null;
            try
            {
                var mek = _hasher.DeriveMek(
                    password,
                    salt,
                    Argon2PasswordHasher.DefaultMemoryKb,
                    Argon2PasswordHasher.DefaultIterations,
                    Argon2PasswordHasher.DefaultParallelism);
                mekBuffer = new SecureBuffer(mek);
                SecureMemory.Memzero(mek);

                var encDbk = _crypto.EncryptParts(mekBuffer.Span, dbk);
                _repo.UpsertVaultMetadata(new VaultMetadataRow
                {
                    Username = username,
                    Salt = salt,
                    ArgonMemory = Argon2PasswordHasher.DefaultMemoryKb,
                    ArgonIterations = Argon2PasswordHasher.DefaultIterations,
                    ArgonParallelism = Argon2PasswordHasher.DefaultParallelism,
                    EncDbk = encDbk.Ciphertext,
                    DbkNonce = encDbk.Nonce,
                    DbkTag = encDbk.Tag,
                    CreatedAt = DateTime.UtcNow.ToString("O")
                });

                foreach (var record in legacyKeys)
                {
                    var keyPath = _paths.GetEncryptedKeyPath(record.Id);
                    if (!File.Exists(keyPath))
                    {
                        continue;
                    }

                    var encrypted = await File.ReadAllBytesAsync(keyPath, cancellationToken).ConfigureAwait(false);
                    var plain = _crypto.Decrypt(oldMaster!, encrypted);
                    try
                    {
                        var parts = _crypto.EncryptParts(dbk, plain);
                        var now = DateTime.UtcNow.ToString("O");
                        _repo.UpsertKey(new SshKeyRow
                        {
                            Id = record.Id.ToString("N"),
                            Name = record.Name,
                            KeyType = KeyraRepository.ToKeyType(record.Algorithm),
                            PublicKey = record.PublicKey,
                            FingerprintSha256 = record.Fingerprint,
                            EncPrivateKey = parts.Ciphertext,
                            PrivateKeyNonce = parts.Nonce,
                            PrivateKeyTag = parts.Tag,
                            Comment = record.Comment,
                            CreatedAt = record.CreatedUtc == default
                                ? now
                                : record.CreatedUtc.ToUniversalTime().ToString("O"),
                            UpdatedAt = now
                        });
                    }
                    finally
                    {
                        SecureMemory.Memzero(plain);
                    }
                }

                await MigrateLegacyConnectionsAsync(cancellationToken).ConfigureAwait(false);

                var migrated = MapKeys(_repo.ListKeys());
                await _keyGarage.WriteAsync(dbk, username, migrated, cancellationToken).ConfigureAwait(false);
                UpdateIntegrityHmac(dbk, username, migrated);

                try
                {
                    await File.WriteAllTextAsync(
                        _paths.LegacyMigratedMarkerPath,
                        DateTime.UtcNow.ToString("O"),
                        cancellationToken).ConfigureAwait(false);
                    File.Move(_paths.MasterKeyFilePath, _paths.MasterKeyFilePath + ".bak", overwrite: true);
                }
                catch
                {
                    // Non-fatal; SQLite vault is authoritative after migration.
                }

                _session.Unlock(username, dbk);
            }
            finally
            {
                mekBuffer?.Dispose();
                SecureMemory.Memzero(dbk);
            }
        }
        finally
        {
            if (oldMaster is not null)
            {
                SecureMemory.Memzero(oldMaster);
            }
        }
    }

    private async Task MigrateLegacyConnectionsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.ConnectionsFilePath))
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_paths.ConnectionsFilePath);
            var document = await JsonSerializer
                .DeserializeAsync<LegacyConnectionsDocument>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            foreach (var profile in document?.Profiles ?? [])
            {
                if (profile is null || profile.Id == Guid.Empty)
                {
                    continue;
                }

                _repo.UpsertServer(new ServerRow
                {
                    Id = profile.Id.ToString("N"),
                    Title = profile.Name ?? string.Empty,
                    Host = profile.Host ?? string.Empty,
                    Port = profile.Port is >= 1 and <= 65535 ? profile.Port : 22,
                    Username = profile.Username ?? string.Empty,
                    DefaultKeyId = profile.VaultKeyId is { } keyId ? keyId.ToString("N") : null,
                    AuthMode = profile.AuthMode == SshAuthMode.Password ? "password" : "key",
                    IsFavorite = profile.IsFavorite,
                    LastConnectedAt = profile.LastConnectedUtc?.ToUniversalTime().ToString("O"),
                    CreatedAt = DateTime.UtcNow.ToString("O")
                });
            }
        }
        catch
        {
            // Best-effort migration of connection profiles.
        }
    }

    private void UpdateIntegrityHmac(byte[] dbk, string username, IReadOnlyList<SshKeyRecord> keys)
    {
        var entries = keys.Select(k => new KeyGarageEntry(
            k.Id.ToString("N"),
            k.Fingerprint,
            CryptoUtilities.ComputeSha256(k.PublicKey))).ToList();
        var payload = CryptoUtilities.BuildKeyGaragePayload(username, entries);
        var hmac = CryptoUtilities.ComputeHmacSha256(dbk, payload);
        _repo.UpdateIntegrityHmac(hmac);
    }

    private static void VerifyIntegrityHmac(
        VaultMetadataRow meta,
        byte[] dbk,
        string username,
        IReadOnlyList<SshKeyRecord> keys)
    {
        if (meta.IntegrityHmac is null || meta.IntegrityHmac.Length == 0)
        {
            return;
        }

        var entries = keys.Select(k => new KeyGarageEntry(
            k.Id.ToString("N"),
            k.Fingerprint,
            CryptoUtilities.ComputeSha256(k.PublicKey))).ToList();
        var payload = CryptoUtilities.BuildKeyGaragePayload(username, entries);
        var computed = CryptoUtilities.ComputeHmacSha256(dbk, payload);
        if (!CryptoUtilities.FixedTimeEquals(meta.IntegrityHmac, computed))
        {
            throw new InvalidOperationException(
                "Vault integrity check failed (metadata HMAC mismatch). Data may be tampered or corrupted.");
        }
    }

    private async Task<IReadOnlyList<SshKeyRecord>> ReadLegacyIndexAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.IndexPath))
        {
            return Array.Empty<SshKeyRecord>();
        }

        await using var fs = new FileStream(_paths.IndexPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var index = await JsonSerializer.DeserializeAsync<VaultIndex>(fs, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return index?.Keys ?? [];
    }

    private static IReadOnlyList<SshKeyRecord> MapKeys(IReadOnlyList<SshKeyRow> rows) =>
        rows.Select(MapKey).ToList();

    internal static SshKeyRecord MapKey(SshKeyRow row)
    {
        _ = DateTime.TryParse(
            row.CreatedAt,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var created);
        return new SshKeyRecord
        {
            Id = Guid.Parse(row.Id),
            Name = row.Name,
            Algorithm = KeyraRepository.FromKeyType(row.KeyType),
            Comment = row.Comment ?? string.Empty,
            PublicKey = row.PublicKey,
            Fingerprint = row.FingerprintSha256,
            CreatedUtc = created == default ? DateTime.UtcNow : created.ToUniversalTime(),
            HasPassphrase = row.EncPassphrase is { Length: > 0 }
        };
    }

    private sealed class LegacyConnectionsDocument
    {
        public List<SshConnectionProfile?>? Profiles { get; set; }
    }
}
