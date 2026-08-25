using SshKeyManager.Models;

namespace SshKeyManager.Services;

public interface IKeyExportService
{
    Task ExportToFolderAsync(
        SshKeyRecord record,
        SecureKeyMaterial privateKey,
        string folderPath,
        bool overwrite,
        CancellationToken cancellationToken = default);

    Task ExportToUserSshAsync(
        SshKeyRecord record,
        SecureKeyMaterial privateKey,
        bool overwrite,
        CancellationToken cancellationToken = default);

    string GetDefaultSshDirectory();
}
