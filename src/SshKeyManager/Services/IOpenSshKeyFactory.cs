using SshKeyManager.Models;

namespace SshKeyManager.Services;

public interface IOpenSshKeyFactory
{
    Task<GeneratedSshKey> GenerateAsync(GenerateKeyRequest request, CancellationToken cancellationToken = default);

    ImportedKey ParsePrivateKey(string pem, string? passphrase);
}
