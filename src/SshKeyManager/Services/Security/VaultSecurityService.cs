using System.Security.Cryptography;
using System.Text.Json;
using SshKeyManager.Models;

namespace SshKeyManager.Services.Security;

public interface IVaultSecurityService
{
    bool IsSetupComplete { get; }

    string RootDirectory { get; }

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
    private readonly Argon2PasswordHasher _hasher;
    private readonly AesGcmVaultCrypto _crypto;
    private readonly KeyGarageHashService _keyGarage;
    private readonly IVaultSession _session;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public VaultSecurityService(
        VaultPaths paths,
        Argon2PasswordHasher hasher,
        AesGcmVaultCrypto crypto,
        KeyGarageHashService keyGarage,
        IVaultSession session)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
        _keyGarage = keyGarage ?? throw new ArgumentNullException(nameof(keyGarage));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public bool IsSetupComplete => File.Exists(_paths.MasterKeyFilePath);

    public string RootDirectory => _paths.RootDirectory;

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

        if (IsSetupComplete)
        {
            throw new InvalidOperationException("Vault setup is already complete.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _paths.EnsureDirectories();

            var masterKey = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.MasterKeySize);
            var passwordSalt = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.SaltSize);
            var passwordHash = _hasher.HashPassword(password, passwordSalt);
            var kekSalt = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.SaltSize);
            var kek = _hasher.DeriveKek(username, password, kekSalt);
            byte[] encryptedMaster;
            try
            {
                encryptedMaster = _crypto.EncryptMasterKey(kek, masterKey);
            }
            finally
            {
                CryptoUtilities.CryptographicClear(kek);
            }

            var fileData = new MasterKeyFileData
            {
                Username = username,
                PasswordVerifierSalt = passwordSalt,
                PasswordVerifierHash = passwordHash,
                KekSalt = kekSalt,
                EncryptedMasterKey = encryptedMaster
            };

            var serialized = MasterKeyFileFormat.Serialize(fileData);
            await File.WriteAllBytesAsync(_paths.MasterKeyFilePath, serialized, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(_paths.IndexPath))
            {
                await WriteEmptyIndexAsync(cancellationToken).ConfigureAwait(false);
            }

            var keys = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            await _keyGarage.WriteAsync(masterKey, username, keys, cancellationToken).ConfigureAwait(false);
            _session.Unlock(username, masterKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ArgumentException)
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

            byte[] masterKey;
            var kek = _hasher.DeriveKek(username, password, fileData.KekSalt);
            try
            {
                masterKey = _crypto.DecryptMasterKey(kek, fileData.EncryptedMasterKey);
            }
            catch (CryptographicException)
            {
                throw new UnauthorizedAccessException("Invalid username or password.");
            }
            finally
            {
                CryptoUtilities.CryptographicClear(kek);
            }

            var keys = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            await _keyGarage.VerifyAsync(masterKey, username, keys, cancellationToken).ConfigureAwait(false);
            _session.Unlock(username, masterKey);
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
            var fileBytes = await File.ReadAllBytesAsync(_paths.MasterKeyFilePath, cancellationToken).ConfigureAwait(false);
            var fileData = MasterKeyFileFormat.Deserialize(fileBytes);

            if (!string.Equals(fileData.Username, username, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Session username does not match vault metadata.");
            }

            if (!_hasher.VerifyPassword(currentPassword, fileData.PasswordVerifierSalt, fileData.PasswordVerifierHash))
            {
                throw new UnauthorizedAccessException("Current password is incorrect.");
            }

            var masterKey = _session.MasterKey;
            var newPasswordSalt = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.SaltSize);
            var newPasswordHash = _hasher.HashPassword(newPassword, newPasswordSalt);
            var newKekSalt = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.SaltSize);
            var newKek = _hasher.DeriveKek(username, newPassword, newKekSalt);
            byte[] encryptedMaster;
            try
            {
                encryptedMaster = _crypto.EncryptMasterKey(newKek, masterKey);
            }
            finally
            {
                CryptoUtilities.CryptographicClear(newKek);
            }

            var updated = new MasterKeyFileData
            {
                Username = username,
                PasswordVerifierSalt = newPasswordSalt,
                PasswordVerifierHash = newPasswordHash,
                KekSalt = newKekSalt,
                EncryptedMasterKey = encryptedMaster
            };

            var serialized = MasterKeyFileFormat.Serialize(updated);
            var temp = _paths.MasterKeyFilePath + ".tmp";
            await File.WriteAllBytesAsync(temp, serialized, cancellationToken).ConfigureAwait(false);
            File.Copy(temp, _paths.MasterKeyFilePath, overwrite: true);
            File.Delete(temp);

            var keys = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            await _keyGarage.WriteAsync(masterKey, username, keys, cancellationToken).ConfigureAwait(false);
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

    private async Task WriteEmptyIndexAsync(CancellationToken cancellationToken)
    {
        var temp = _paths.IndexPath + ".tmp";
        await using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            await JsonSerializer.SerializeAsync(fs, new VaultIndex(), JsonOptions, cancellationToken).ConfigureAwait(false);
            await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Copy(temp, _paths.IndexPath, overwrite: true);
        File.Delete(temp);
    }

    private async Task<IReadOnlyList<SshKeyRecord>> ReadIndexAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.IndexPath))
        {
            return Array.Empty<SshKeyRecord>();
        }

        await using var fs = new FileStream(_paths.IndexPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var index = await JsonSerializer.DeserializeAsync<VaultIndex>(fs, JsonOptions, cancellationToken).ConfigureAwait(false);
        return index?.Keys ?? [];
    }
}
