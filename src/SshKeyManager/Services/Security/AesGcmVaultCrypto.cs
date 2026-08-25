using System.Security.Cryptography;

namespace SshKeyManager.Services.Security;

public sealed class AesGcmParts
{
    public required byte[] Ciphertext { get; init; }

    public required byte[] Nonce { get; init; }

    public required byte[] Tag { get; init; }
}

public sealed class AesGcmVaultCrypto
{
    public AesGcmParts EncryptParts(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plainBytes)
    {
        EnsureKey(key);

        var nonce = CryptoUtilities.GenerateRandomBytes(CryptoUtilities.NonceSize);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[CryptoUtilities.TagSize];

        using var aes = new AesGcm(key, CryptoUtilities.TagSize);
        aes.Encrypt(nonce, plainBytes, ciphertext, tag);

        return new AesGcmParts
        {
            Ciphertext = ciphertext,
            Nonce = nonce,
            Tag = tag
        };
    }

    public byte[] DecryptParts(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> tag)
    {
        EnsureKey(key);
        if (nonce.Length != CryptoUtilities.NonceSize)
        {
            throw new CryptographicException("Invalid nonce size.");
        }

        if (tag.Length != CryptoUtilities.TagSize)
        {
            throw new CryptographicException("Invalid auth tag size.");
        }

        var plain = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, CryptoUtilities.TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plain);
        return plain;
    }

    public byte[] Encrypt(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> plainBytes)
    {
        var parts = EncryptParts(masterKey, plainBytes);
        var result = new byte[CryptoUtilities.NonceSize + CryptoUtilities.TagSize + parts.Ciphertext.Length];
        Buffer.BlockCopy(parts.Nonce, 0, result, 0, parts.Nonce.Length);
        Buffer.BlockCopy(parts.Tag, 0, result, parts.Nonce.Length, parts.Tag.Length);
        Buffer.BlockCopy(parts.Ciphertext, 0, result, parts.Nonce.Length + parts.Tag.Length, parts.Ciphertext.Length);
        return result;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> encryptedBlob)
    {
        EnsureKey(masterKey);

        if (encryptedBlob.Length < CryptoUtilities.NonceSize + CryptoUtilities.TagSize)
        {
            throw new CryptographicException("Encrypted blob is too short.");
        }

        var nonce = encryptedBlob[..CryptoUtilities.NonceSize];
        var tag = encryptedBlob.Slice(CryptoUtilities.NonceSize, CryptoUtilities.TagSize);
        var ciphertext = encryptedBlob[(CryptoUtilities.NonceSize + CryptoUtilities.TagSize)..];
        return DecryptParts(masterKey, ciphertext, nonce, tag);
    }

    public byte[] EncryptMasterKey(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> masterKey)
    {
        EnsureKey(kek);
        return Encrypt(kek, masterKey);
    }

    public byte[] DecryptMasterKey(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> encryptedMasterKey)
    {
        EnsureKey(kek);
        return Decrypt(kek, encryptedMasterKey);
    }

    private static void EnsureKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != CryptoUtilities.MasterKeySize)
        {
            throw new ArgumentException("Key must be 256 bits.", nameof(key));
        }
    }
}
