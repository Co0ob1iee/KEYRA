using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SshKeyManager.Models;
using SshKeyManager.Services.Security;

namespace SshKeyManager.Services;

public sealed class VaultStore : IVaultStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly VaultPaths _paths;
    private readonly IVaultSession _session;
    private readonly AesGcmVaultCrypto _crypto;
    private readonly KeyGarageHashService _keyGarage;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public VaultStore(
        VaultPaths paths,
        IVaultSession session,
        AesGcmVaultCrypto crypto,
        KeyGarageHashService keyGarage)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
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
            EnsureVault();
            var index = await ReadIndexUnlockedAsync(cancellationToken).ConfigureAwait(false);
            return index.Keys
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
            EnsureVault();
            var index = await ReadIndexUnlockedAsync(cancellationToken).ConfigureAwait(false);
            return index.Keys.FirstOrDefault(k => k.Id == id);
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
            EnsureVault();
            var index = await ReadIndexUnlockedAsync(cancellationToken).ConfigureAwait(false);

            if (metadata.Id == Guid.Empty)
            {
                metadata.Id = Guid.NewGuid();
            }

            if (metadata.CreatedUtc == default)
            {
                metadata.CreatedUtc = DateTime.UtcNow;
            }

            var plain = Encoding.UTF8.GetBytes(privateKeyPem);
            byte[] encrypted;
            try
            {
                encrypted = _crypto.Encrypt(_session.MasterKey, plain);
            }
            finally
            {
                CryptoUtilities.CryptographicClear(plain);
            }

            var keyPath = _paths.GetEncryptedKeyPath(metadata.Id);
            await using (var fs = new FileStream(
                             keyPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await fs.WriteAsync(encrypted, cancellationToken).ConfigureAwait(false);
                await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var existing = index.Keys.FindIndex(k => k.Id == metadata.Id);
            if (existing >= 0)
            {
                index.Keys[existing] = CloneRecord(metadata);
            }
            else
            {
                index.Keys.Add(CloneRecord(metadata));
            }

            await WriteIndexUnlockedAsync(index, cancellationToken).ConfigureAwait(false);
            await _keyGarage.WriteAsync(_session.MasterKey, _session.Username, index.Keys, cancellationToken)
                .ConfigureAwait(false);
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
            EnsureVault();
            var index = await ReadIndexUnlockedAsync(cancellationToken).ConfigureAwait(false);
            if (index.Keys.All(k => k.Id != id))
            {
                throw new FileNotFoundException("Key metadata was not found in the vault index.", id.ToString());
            }

            var keyPath = _paths.GetEncryptedKeyPath(id);
            if (!File.Exists(keyPath))
            {
                throw new FileNotFoundException("Encrypted private key file is missing.", keyPath);
            }

            var encrypted = await File.ReadAllBytesAsync(keyPath, cancellationToken).ConfigureAwait(false);
            var plain = _crypto.Decrypt(_session.MasterKey, encrypted);
            try
            {
                var pem = Encoding.UTF8.GetString(plain);
                return new SecureKeyMaterial(pem);
            }
            finally
            {
                CryptoUtilities.CryptographicClear(plain);
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
            EnsureVault();
            var index = await ReadIndexUnlockedAsync(cancellationToken).ConfigureAwait(false);
            index.Keys.RemoveAll(k => k.Id == id);
            await WriteIndexUnlockedAsync(index, cancellationToken).ConfigureAwait(false);

            var keyPath = _paths.GetEncryptedKeyPath(id);
            if (File.Exists(keyPath))
            {
                File.Delete(keyPath);
            }

            await _keyGarage.WriteAsync(_session.MasterKey, _session.Username, index.Keys, cancellationToken)
                .ConfigureAwait(false);
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

    private void EnsureUnlocked()
    {
        if (!_session.IsUnlocked)
        {
            throw new InvalidOperationException("Vault is locked.");
        }
    }

    private void EnsureVault()
    {
        _paths.EnsureDirectories();
    }

    private async Task<VaultIndex> ReadIndexUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.IndexPath))
        {
            return new VaultIndex();
        }

        await using var fs = new FileStream(_paths.IndexPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var index = await JsonSerializer.DeserializeAsync<VaultIndex>(fs, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return index ?? new VaultIndex();
    }

    private async Task WriteIndexUnlockedAsync(VaultIndex index, CancellationToken cancellationToken)
    {
        var temp = _paths.IndexPath + ".tmp";
        await using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            await JsonSerializer.SerializeAsync(fs, index, JsonOptions, cancellationToken).ConfigureAwait(false);
            await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Copy(temp, _paths.IndexPath, overwrite: true);
        File.Delete(temp);
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
