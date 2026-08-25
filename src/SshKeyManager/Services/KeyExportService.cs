using System.Text;
using SshKeyManager.Helpers;
using SshKeyManager.Models;

namespace SshKeyManager.Services;

public sealed class KeyExportService : IKeyExportService
{
    public string GetDefaultSshDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");

    public async Task ExportToFolderAsync(
        SshKeyRecord record,
        SecureKeyMaterial privateKey,
        string folderPath,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(privateKey);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Folder path is required.", nameof(folderPath));
        }

        try
        {
            Directory.CreateDirectory(folderPath);
            var baseName = FileNameSanitizer.Sanitize(record.Name);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "id_ssh";
            }

            var privatePath = Path.Combine(folderPath, baseName);
            var publicPath = privatePath + ".pub";

            if (!overwrite)
            {
                if (File.Exists(privatePath) || File.Exists(publicPath))
                {
                    throw new IOException(
                        $"Export target already exists:\n{privatePath}\n{publicPath}\nEnable overwrite to replace.");
                }
            }

            var pem = privateKey.GetPrivateKeyPem();
            await File.WriteAllTextAsync(privatePath, pem.TrimEnd() + Environment.NewLine, new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);
            await File.WriteAllTextAsync(publicPath, record.PublicKey.TrimEnd() + Environment.NewLine, new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);

            try
            {
                var info = new FileInfo(privatePath);
                info.Attributes |= FileAttributes.Hidden;
            }
            catch (Exception)
            {
                // Attribute is best-effort on Windows.
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not IOException)
        {
            throw new InvalidOperationException("Failed to export key files.", ex);
        }
    }

    public Task ExportToUserSshAsync(
        SshKeyRecord record,
        SecureKeyMaterial privateKey,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        var sshDir = GetDefaultSshDirectory();
        return ExportToFolderAsync(record, privateKey, sshDir, overwrite, cancellationToken);
    }
}
