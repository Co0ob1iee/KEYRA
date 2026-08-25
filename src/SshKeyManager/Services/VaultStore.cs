using System.Security.Cryptography;
using System.Text;
using SshKeyManager.Models;
using SshKeyManager.Services.Data;
using SshKeyManager.Services.Security;

namespace SshKeyManager.Services;

public sealed class VaultStore : IVaultStore
{
    private readonly VaultPaths _paths;
    private readonly KeyraRepository _repo;
    private readonly IVaultSession _session;
    private readonly AesGcmVaultCrypto _crypto;
    private readonly KeyGarageHashService _keyGarage;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public VaultStore(
        VaultPaths paths,
        KeyraRepository repo,
        IVaultSession session,
        AesGcmVaultCrypto crypto,
        KeyGarageHashService keyGarage)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
        _keyGarage = keyGarage ?? throw new ArgumentNullException(nameof(keyGarage));
    }

    public string VaultDirectory => _paths.VaultDirectory;

    public string RootDirectory => _paths.RootDirectory;

    public async Task<IReadOnlyList<SshKeyRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _repo.ListKeys()
                .Select(VaultSecurityService.MapKey)
                .OrderByDescending(k => k.CreatedUtc)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SshKeyRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var row = _repo.GetKey(id.ToString("N"));
            return row is null ? null : VaultSecurityService.MapKey(row);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SshKeyRecord> SaveAsync(
        SshKeyRecord metadata,
        string privateKeyPem,
        CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        ArgumentNullException.ThrowIfNull(metadata);
        if (string.IsNullOrWhiteSpace(privateKeyPem))
        {
            throw new ArgumentException("Private key PEM is required.", nameof(privateKeyPem));
        }

        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            throw new ArgumentException("Key name is required.", nameof(metadata));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (metadata.Id == Guid.Empty)
            {
                metadata.Id = Guid.NewGuid();
            }

            if (metadata.CreatedUtc == default)
            {
                metadata.CreatedUtc = DateTime.UtcNow;
            }

            var plain = Encoding.UTF8.GetBytes(privateKeyPem);
            AesGcmParts parts;
            try
            {
                parts = _crypto.EncryptParts(_session.DatabaseKey, plain);
            }
            finally
            {
                SecureMemory.Memzero(plain);
            }

            var now = DateTime.UtcNow.ToString("O");
            _repo.UpsertKey(new SshKeyRow
            {
                Id = metadata.Id.ToString("N"),
                Name = metadata.Name,
                KeyType = KeyraRepository.ToKeyType(metadata.Algorithm),
                PublicKey = metadata.PublicKey,
                FingerprintSha256 = metadata.Fingerprint,
                EncPrivateKey = parts.Ciphertext,
                PrivateKeyNonce = parts.Nonce,
                PrivateKeyTag = parts.Tag,
                Comment = metadata.Comment,
                CreatedAt = metadata.CreatedUtc.ToUniversalTime().ToString("O"),
                UpdatedAt = now
            });

            var keys = _repo.ListKeys().Select(VaultSecurityService.MapKey).ToList();
            await _keyGarage.WriteAsync(_session.DatabaseKey, _session.Username, keys, cancellationToken)
                .ConfigureAwait(false);
            RefreshIntegrityHmac(keys);
            return CloneRecord(metadata);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Failed to save key '{metadata.Name}' to vault.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SecureKeyMaterial> LoadPrivateKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var row = _repo.GetKey(id.ToString("N"))
                ?? throw new FileNotFoundException("Key metadata was not found in the vault.", id.ToString());

            var plain = _crypto.DecryptParts(
                _session.DatabaseKey,
                row.EncPrivateKey,
                row.PrivateKeyNonce,
                row.PrivateKeyTag);
            try
            {
                var pem = Encoding.UTF8.GetString(plain);
                return new SecureKeyMaterial(pem);
            }
            finally
            {
                SecureMemory.Memzero(plain);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not FileNotFoundException and not CryptographicException)
        {
            throw new InvalidOperationException("Failed to load private key from vault.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _repo.DeleteKey(id.ToString("N"));
            var keys = _repo.ListKeys().Select(VaultSecurityService.MapKey).ToList();
            await _keyGarage.WriteAsync(_session.DatabaseKey, _session.Username, keys, cancellationToken)
                .ConfigureAwait(false);
            RefreshIntegrityHmac(keys);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("Failed to delete key from vault.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void RefreshIntegrityHmac(IReadOnlyList<SshKeyRecord> keys)
    {
        var entries = keys.Select(k => new KeyGarageEntry(
            k.Id.ToString("N"),
            k.Fingerprint,
            CryptoUtilities.ComputeSha256(k.PublicKey))).ToList();
        var payload = CryptoUtilities.BuildKeyGaragePayload(_session.Username, entries);
        var hmac = CryptoUtilities.ComputeHmacSha256(_session.DatabaseKey, payload);
        _repo.UpdateIntegrityHmac(hmac);
    }

    private void EnsureUnlocked()
    {
        if (!_session.IsUnlocked)
        {
            throw new InvalidOperationException("Vault is locked.");
        }
    }

    private static SshKeyRecord CloneRecord(SshKeyRecord source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Algorithm = source.Algorithm,
        Comment = source.Comment,
        PublicKey = source.PublicKey,
        Fingerprint = source.Fingerprint,
        CreatedUtc = source.CreatedUtc,
        HasPassphrase = source.HasPassphrase
    };
}
