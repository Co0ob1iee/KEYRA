using System.Diagnostics;
using SshKeyManager.Models;
using SshKeyManager.Services.Security;

namespace SshKeyManager.Services.Hardware;

public sealed class HardwareSecurityKeyInfo
{
    public Guid Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Backend { get; init; } = "fido2";

    public string PublicKey { get; init; } = string.Empty;

    public string Fingerprint { get; init; } = string.Empty;

    public bool TouchRequired { get; init; } = true;

    public bool IsActive { get; init; } = true;

    public string StatusText { get; init; } = "Ready";
}

public interface IHardwareKeyService
{
    bool IsFido2PairingAvailable { get; }

    string AvailabilityMessage { get; }

    Task<IReadOnlyList<HardwareSecurityKeyInfo>> ListAsync(CancellationToken cancellationToken = default);

    Task<HardwareSecurityKeyInfo> PairSkEd25519Async(
        string displayName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> TestTouchAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Hardware security key integration. Prefer real FIDO2 via OpenSSH ssh-keygen -t ed25519-sk when available.
/// Stores public key + key handle PEM in the vault as sk-ed25519 entries.
/// </summary>
public sealed class HardwareKeyService : IHardwareKeyService
{
    private readonly IVaultStore _vault;
    private readonly IOpenSshKeyFactory _keys;
    private readonly IAppLogService _log;
    private readonly VaultPaths _paths;

    public HardwareKeyService(
        IVaultStore vault,
        IOpenSshKeyFactory keys,
        IAppLogService log,
        VaultPaths paths)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public bool IsFido2PairingAvailable => FindSshKeygen() is not null;

    public string AvailabilityMessage => IsFido2PairingAvailable
        ? "FIDO2 pairing uses OpenSSH ssh-keygen (ed25519-sk). Touch the security key when prompted. "
          + "Paired keys are for OpenSSH CLI / system agent auth — KEYRA in-app SSH sessions cannot perform FIDO2 SK authentication (SSH.NET limitation)."
        : "FIDO2 pairing requires OpenSSH ssh-keygen on PATH (Windows OpenSSH). Install OpenSSH Client, then retry. "
          + "PKCS#11 PIV is planned for a later release. In-app SSH connect does not support sk-ed25519.";

    public async Task<IReadOnlyList<HardwareSecurityKeyInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var keys = await _vault.ListAsync(cancellationToken).ConfigureAwait(false);
        return keys
            .Where(k => k.Algorithm == SshKeyAlgorithm.SkEd25519)
            .Select(k => new HardwareSecurityKeyInfo
            {
                Id = k.Id,
                DisplayName = k.Name,
                Backend = "fido2",
                PublicKey = k.PublicKey,
                Fingerprint = k.Fingerprint,
                TouchRequired = true,
                IsActive = true,
                StatusText = "Ready"
            })
            .ToList();
    }

    public async Task<HardwareSecurityKeyInfo> PairSkEd25519Async(
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var sshKeygen = FindSshKeygen()
            ?? throw new InvalidOperationException(AvailabilityMessage);

        var workDir = Path.Combine(_paths.RootDirectory, "hardware-tmp");
        Directory.CreateDirectory(workDir);
        var keyPath = Path.Combine(workDir, $"sk-{Guid.NewGuid():N}");
        var pubPath = keyPath + ".pub";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = sshKeygen,
                ArgumentList =
                {
                    "-t", "ed25519-sk",
                    "-f", keyPath,
                    "-N", "",
                    "-C", displayName.Trim(),
                    "-q"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start ssh-keygen.");
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(keyPath) || !File.Exists(pubPath))
            {
                var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new InvalidOperationException(
                    "FIDO2 key generation failed. Ensure a YubiKey (or other FIDO2 key) is connected and touch when prompted. "
                    + detail.Trim());
            }

            var privatePem = await File.ReadAllTextAsync(keyPath, cancellationToken).ConfigureAwait(false);
            var publicLine = (await File.ReadAllTextAsync(pubPath, cancellationToken).ConfigureAwait(false)).Trim();

            // Persist handle/private OpenSSH sk blob encrypted in vault (hardware holds the secret).
            var fingerprint = ComputeFingerprintFromPublicLine(publicLine);
            var record = new SshKeyRecord
            {
                Id = Guid.NewGuid(),
                Name = displayName.Trim(),
                Algorithm = SshKeyAlgorithm.SkEd25519,
                Comment = displayName.Trim(),
                PublicKey = publicLine,
                Fingerprint = fingerprint,
                CreatedUtc = DateTime.UtcNow,
                HasPassphrase = false
            };

            await _vault.SaveAsync(record, privatePem, cancellationToken).ConfigureAwait(false);
            _log.Info($"Paired hardware key '{record.Name}' ({record.Fingerprint}).");

            return new HardwareSecurityKeyInfo
            {
                Id = record.Id,
                DisplayName = record.Name,
                Backend = "fido2",
                PublicKey = record.PublicKey,
                Fingerprint = record.Fingerprint,
                TouchRequired = true,
                IsActive = true,
                StatusText = "Ready"
            };
        }
        finally
        {
            TryDelete(keyPath);
            TryDelete(pubPath);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _vault.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> TestTouchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Presence check: ensure sk key material can be loaded from vault (touch happens at SSH auth time).
        return TestTouchCoreAsync(id, cancellationToken);
    }

    private async Task<bool> TestTouchCoreAsync(Guid id, CancellationToken cancellationToken)
    {
        using var material = await _vault.LoadPrivateKeyAsync(id, cancellationToken).ConfigureAwait(false);
        var pem = material.GetPrivateKeyPem();
        return pem.Contains("OPENSSH PRIVATE KEY", StringComparison.Ordinal);
    }

    private static string? FindSshKeygen()
    {
        var candidates = new[]
        {
            "ssh-keygen",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"OpenSSH\ssh-keygen.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"OpenSSH\ssh-keygen.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\OpenSSH\ssh-keygen.exe")
        };

        foreach (var candidate in candidates)
        {
            try
            {
                if (candidate.Equals("ssh-keygen", StringComparison.Ordinal))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = candidate,
                        ArgumentList = { "-V" },
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p is null)
                    {
                        continue;
                    }

                    p.WaitForExit(1500);
                    if (p.ExitCode == 0 || p.ExitCode == 1)
                    {
                        return candidate;
                    }
                }
                else if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // try next
            }
        }

        return null;
    }

    private static string ComputeFingerprintFromPublicLine(string publicLine)
    {
        var parts = publicLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return "SHA256:";
        }

        var blob = Convert.FromBase64String(parts[1]);
        var hash = System.Security.Cryptography.SHA256.HashData(blob);
        return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of temporary sk files.
        }
    }
}
