using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;

namespace SshKeyManager.Services.OpenSsh;

internal static class OpenSshPemCodec
{
    private const string AuthMagic = "openssh-key-v1\0";
    private const string BeginMarker = "-----BEGIN OPENSSH PRIVATE KEY-----";
    private const string EndMarker = "-----END OPENSSH PRIVATE KEY-----";
    private const int DefaultBcryptRounds = 16;

    public static string WrapPem(byte[] blob)
    {
        ArgumentNullException.ThrowIfNull(blob);
        var base64 = Convert.ToBase64String(blob);
        var sb = new StringBuilder();
        sb.AppendLine(BeginMarker);
        for (var i = 0; i < base64.Length; i += 70)
        {
            var len = Math.Min(70, base64.Length - i);
            sb.AppendLine(base64.Substring(i, len));
        }

        sb.Append(EndMarker);
        return sb.ToString();
    }

    public static byte[] UnwrapPem(string pem)
    {
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new ArgumentException("Private key PEM is empty.", nameof(pem));
        }

        var lines = pem
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var started = false;
        var base64 = new StringBuilder();
        foreach (var line in lines)
        {
            if (line.StartsWith("-----BEGIN", StringComparison.Ordinal))
            {
                if (!line.Contains("OPENSSH PRIVATE KEY", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Only OpenSSH private keys are supported.");
                }

                started = true;
                continue;
            }

            if (line.StartsWith("-----END", StringComparison.Ordinal))
            {
                break;
            }

            if (started)
            {
                base64.Append(line);
            }
        }

        if (!started || base64.Length == 0)
        {
            throw new InvalidDataException("Invalid OpenSSH private key PEM.");
        }

        try
        {
            return Convert.FromBase64String(base64.ToString());
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Invalid Base64 in OpenSSH private key.", ex);
        }
    }

    public static string EncodePrivateKey(
        Org.BouncyCastle.Crypto.AsymmetricKeyParameter privateKey,
        string? passphrase)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        var unencrypted = OpenSshPrivateKeyUtilities.EncodePrivateKey(privateKey);
        return EncodeUnencryptedBlob(unencrypted, passphrase);
    }

    public static string EncodeUnencryptedBlob(byte[] unencryptedBlob, string? passphrase)
    {
        ArgumentNullException.ThrowIfNull(unencryptedBlob);
        if (string.IsNullOrEmpty(passphrase))
        {
            return WrapPem(unencryptedBlob);
        }

        var encrypted = EncryptPrivateKeyBlob(unencryptedBlob, passphrase);
        return WrapPem(encrypted);
    }

    public static Org.BouncyCastle.Crypto.AsymmetricKeyParameter DecodePrivateKey(
        string pem,
        string? passphrase)
    {
        var blob = UnwrapPem(pem);
        var decrypted = DecryptPrivateKeyBlob(blob, passphrase);
        try
        {
            return OpenSshPrivateKeyUtilities.ParsePrivateKeyBlob(decrypted);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("Unable to parse OpenSSH private key. Check passphrase and format.", ex);
        }
    }

    public static bool IsEncrypted(string pem)
    {
        var blob = UnwrapPem(pem);
        var reader = new SshBufferReader(blob);
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(15));
        if (!string.Equals(magic, AuthMagic, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Not an openssh-key-v1 private key.");
        }

        var cipherName = reader.ReadUtf8String();
        return !string.Equals(cipherName, "none", StringComparison.Ordinal);
    }

    private static byte[] EncryptPrivateKeyBlob(byte[] unencryptedBlob, string passphrase)
    {
        var reader = new SshBufferReader(unencryptedBlob);
        var magic = reader.ReadBytes(15);
        if (!Encoding.ASCII.GetString(magic).Equals(AuthMagic, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unexpected OpenSSH private key magic.");
        }

        _ = reader.ReadUtf8String(); // cipher
        _ = reader.ReadUtf8String(); // kdf
        _ = reader.ReadString(); // kdf options
        var nkeys = reader.ReadUInt32();
        if (nkeys != 1)
        {
            throw new InvalidDataException("Only single-key OpenSSH files are supported.");
        }

        var publicKey = reader.ReadString();
        var privateSection = reader.ReadString();

        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        const int rounds = DefaultBcryptRounds;
        var keyIv = new byte[32 + 16];
        new OpenSshBcryptPbkdf().DeriveKey(Encoding.UTF8.GetBytes(passphrase), salt, rounds, keyIv);
        var key = keyIv.AsSpan(0, 32).ToArray();
        var iv = keyIv.AsSpan(32, 16).ToArray();

        var padded = EnsureBlockPadding(privateSection, 16);
        var cipherText = AesCtrTransform(padded, key, iv, forEncryption: true);

        var kdfOptions = new SshBufferWriter();
        kdfOptions.WriteString(salt);
        kdfOptions.WriteUInt32(rounds);

        var writer = new SshBufferWriter();
        writer.WriteBytes(Encoding.ASCII.GetBytes(AuthMagic));
        writer.WriteString("aes256-ctr");
        writer.WriteString("bcrypt");
        writer.WriteString(kdfOptions.ToArray());
        writer.WriteUInt32(1);
        writer.WriteString(publicKey);
        writer.WriteString(cipherText);

        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(iv);
        CryptographicOperations.ZeroMemory(keyIv);
        return writer.ToArray();
    }

    private static byte[] DecryptPrivateKeyBlob(byte[] blob, string? passphrase)
    {
        var reader = new SshBufferReader(blob);
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(15));
        if (!string.Equals(magic, AuthMagic, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Not an openssh-key-v1 private key.");
        }

        var cipherName = reader.ReadUtf8String();
        var kdfName = reader.ReadUtf8String();
        var kdfOptions = reader.ReadString();
        var nkeys = reader.ReadUInt32();
        if (nkeys != 1)
        {
            throw new InvalidDataException("Only single-key OpenSSH files are supported.");
        }

        var publicKey = reader.ReadString();
        var privateSection = reader.ReadString();

        if (string.Equals(cipherName, "none", StringComparison.Ordinal))
        {
            return RebuildUnencryptedBlob(publicKey, privateSection);
        }

        if (!string.Equals(cipherName, "aes256-ctr", StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Cipher '{cipherName}' is not supported.");
        }

        if (!string.Equals(kdfName, "bcrypt", StringComparison.Ordinal))
        {
            throw new NotSupportedException($"KDF '{kdfName}' is not supported.");
        }

        if (string.IsNullOrEmpty(passphrase))
        {
            throw new InvalidDataException("Private key is encrypted. A passphrase is required.");
        }

        var kdfReader = new SshBufferReader(kdfOptions);
        var salt = kdfReader.ReadString();
        var rounds = (int)kdfReader.ReadUInt32();
        var keyIv = new byte[32 + 16];
        new OpenSshBcryptPbkdf().DeriveKey(Encoding.UTF8.GetBytes(passphrase), salt, rounds, keyIv);
        var key = keyIv.AsSpan(0, 32).ToArray();
        var iv = keyIv.AsSpan(32, 16).ToArray();

        byte[] decrypted;
        try
        {
            decrypted = AesCtrTransform(privateSection, key, iv, forEncryption: false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(iv);
            CryptographicOperations.ZeroMemory(keyIv);
        }

        return RebuildUnencryptedBlob(publicKey, decrypted);
    }

    private static byte[] RebuildUnencryptedBlob(byte[] publicKey, byte[] privateSection)
    {
        var writer = new SshBufferWriter();
        writer.WriteBytes(Encoding.ASCII.GetBytes(AuthMagic));
        writer.WriteString("none");
        writer.WriteString("none");
        writer.WriteString(Array.Empty<byte>());
        writer.WriteUInt32(1);
        writer.WriteString(publicKey);
        writer.WriteString(privateSection);
        return writer.ToArray();
    }

    private static byte[] EnsureBlockPadding(byte[] data, int blockSize)
    {
        if (data.Length % blockSize == 0)
        {
            return data;
        }

        // OpenSSH private section already includes 1,2,3... padding from BC encoder.
        // If length is not aligned (should not happen for none→encrypt), pad with continuing sequence.
        var padLen = blockSize - (data.Length % blockSize);
        var padded = new byte[data.Length + padLen];
        Buffer.BlockCopy(data, 0, padded, 0, data.Length);
        for (var i = 0; i < padLen; i++)
        {
            padded[data.Length + i] = (byte)(i + 1);
        }

        return padded;
    }

    private static byte[] AesCtrTransform(byte[] input, byte[] key, byte[] iv, bool forEncryption)
    {
        var cipher = new StreamBlockCipher(new SicBlockCipher(new AesEngine()));
        cipher.Init(forEncryption, new ParametersWithIV(new KeyParameter(key), iv));
        var output = new byte[input.Length];
        cipher.ProcessBytes(input, 0, input.Length, output, 0);
        return output;
    }
}
