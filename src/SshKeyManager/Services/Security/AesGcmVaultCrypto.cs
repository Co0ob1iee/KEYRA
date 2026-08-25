using System.Security.Cryptography;

namespace SshKeyManager.Services.Security;

public sealed class AesGcmVaultCrypto
{
    public byte[] Encrypt(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> plainBytes)
    {
        if (masterKey.Length != CryptoUtilities.MasterKeySize)
        {
            throw new ArgumentException("Master key must be 256 bits.", nameof(masterKey));
        }

        var nonce = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.NonceSize);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[CryptoUtilities.TagSize];

        using var aes = new AesGcm(masterKey, CryptoUtilities.TagSize);
        aes.Encrypt(nonce, plainBytes, ciphertext, tag);

        var result = new byte[CryptoUtilities.NonceSize + CryptoUtilities.TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
        return result;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> encryptedBlob)
    {
        if (masterKey.Length != CryptoUtilities.MasterKeySize)
        {
            throw new ArgumentException("Master key must be 256 bits.", nameof(masterKey));
        }

        if (encryptedBlob.Length < CryptoUtilities.NonceSize + CryptoUtilities.TagSize)
        {
            throw new CryptographicException("Encrypted blob is too short.");
        }

        var nonce = encryptedBlob[..CryptoUtilities.NonceSize];
        var tag = encryptedBlob.Slice(CryptoUtilities.NonceSize, CryptoUtilities.TagSize);
        var ciphertext = encryptedBlob[(CryptoUtilities.NonceSize + CryptoUtilities.TagSize)..];
        var plain = new byte[ciphertext.Length];

        using var aes = new AesGcm(masterKey, CryptoUtilities.TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plain);
        return plain;
    }

    public byte[] EncryptMasterKey(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> masterKey)
    {
        if (kek.Length != CryptoUtilities.MasterKeySize)
        {
            throw new ArgumentException("KEK must be 256 bits.", nameof(kek));
        }

        return Encrypt(kek, masterKey);
    }

    public byte[] DecryptMasterKey(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> encryptedMasterKey)
    {
        if (kek.Length != CryptoUtilities.MasterKeySize)
        {
            throw new ArgumentException("KEK must be 256 bits.", nameof(kek));
        }

        return Decrypt(kek, encryptedMasterKey);
    }
}
