using System.Text;

namespace SshKeyManager.Models;

/// <summary>
/// Short-lived private key PEM held in memory. Clear after use.
/// </summary>
public sealed class SecureKeyMaterial : IDisposable
{
    private char[]? _privateKeyPem;
    private bool _disposed;

    public SecureKeyMaterial(string privateKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);
        _privateKeyPem = privateKeyPem.ToCharArray();
    }

    public string GetPrivateKeyPem()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_privateKeyPem is null)
        {
            throw new InvalidOperationException("Private key material is not available.");
        }

        return new string(_privateKeyPem);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_privateKeyPem is not null)
        {
            Array.Clear(_privateKeyPem, 0, _privateKeyPem.Length);
            _privateKeyPem = null;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~SecureKeyMaterial()
    {
        Dispose();
    }

    public override string ToString() => "[SecureKeyMaterial]";
}
