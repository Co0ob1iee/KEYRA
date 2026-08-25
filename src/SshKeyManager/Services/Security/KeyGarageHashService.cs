using System.Text;
using SshKeyManager.Models;

namespace SshKeyManager.Services.Security;

public sealed class KeyGarageHashService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("KGH1");
    private const byte Version = 1;

    private readonly VaultPaths _paths;

    public KeyGarageHashService(VaultPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task WriteAsync(byte[] masterKey, string username, IReadOnlyList<SshKeyRecord> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentNullException.ThrowIfNull(keys);

        var entries = keys.Select(k => new KeyGarageEntry(
            k.Id.ToString("N"),
            k.Fingerprint,
            CryptoUtilities.ComputeSha256(k.PublicKey))).ToList();

        var payload = CryptoUtilities.BuildKeyGaragePayload(username, entries);
        var hmac = CryptoUtilities.ComputeHmacSha256(masterKey, payload);

        var fileBytes = new byte[Magic.Length + 1 + hmac.Length];
        Buffer.BlockCopy(Magic, 0, fileBytes, 0, Magic.Length);
        fileBytes[Magic.Length] = Version;
        Buffer.BlockCopy(hmac, 0, fileBytes, Magic.Length + 1, hmac.Length);

        var temp = _paths.KeyGarageHashPath + ".tmp";
        await File.WriteAllBytesAsync(temp, fileBytes, cancellationToken).ConfigureAwait(false);
        File.Copy(temp, _paths.KeyGarageHashPath, overwrite: true);
        File.Delete(temp);
    }

    public async Task VerifyAsync(byte[] masterKey, string username, IReadOnlyList<SshKeyRecord> keys, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_paths.KeyGarageHashPath))
        {
            throw new InvalidOperationException("KeyGarageHash file is missing. Vault integrity cannot be verified.");
        }

        var fileBytes = await File.ReadAllBytesAsync(_paths.KeyGarageHashPath, cancellationToken).ConfigureAwait(false);
        if (fileBytes.Length != Magic.Length + 1 + CryptoUtilities.HmacSize)
        {
            throw new InvalidOperationException("KeyGarageHash file is corrupt.");
        }

        if (!fileBytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new InvalidOperationException("KeyGarageHash file has an invalid signature.");
        }

        if (fileBytes[Magic.Length] != Version)
        {
            throw new InvalidOperationException("Unsupported KeyGarageHash version.");
        }

        var storedHmac = fileBytes.AsSpan(Magic.Length + 1, CryptoUtilities.HmacSize);
        var entries = keys.Select(k => new KeyGarageEntry(
            k.Id.ToString("N"),
            k.Fingerprint,
            CryptoUtilities.ComputeSha256(k.PublicKey))).ToList();

        var payload = CryptoUtilities.BuildKeyGaragePayload(username, entries);
        var computed = CryptoUtilities.ComputeHmacSha256(masterKey, payload);

        if (!CryptoUtilities.FixedTimeEquals(storedHmac, computed))
        {
            throw new InvalidOperationException(
                "Vault integrity check failed (KeyGarageHash mismatch). Data may be tampered or corrupted.");
        }
    }
}
