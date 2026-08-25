using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using SshKeyManager.Models;
using SshKeyManager.Services.OpenSsh;

namespace SshKeyManager.Services;

public sealed class OpenSshKeyFactory : IOpenSshKeyFactory
{
    public Task<GeneratedSshKey> GenerateAsync(GenerateKeyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Key name is required.", nameof(request));
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var keyPair = CreateKeyPair(request.Algorithm);
            var comment = string.IsNullOrWhiteSpace(request.Comment)
                ? BuildDefaultComment()
                : request.Comment.Trim();

            // Embed comment by re-encoding: BC EncodePrivateKey uses empty comment.
            // We inject comment into unencrypted private section when no passphrase,
            // and for passphrase path we build from a comment-aware unencrypted blob.
            var privatePem = EncodeWithComment(keyPair.Private, comment, request.Passphrase);
            var publicKey = FormatPublicKey(keyPair.Public, comment);
            var fingerprint = ComputeFingerprint(keyPair.Public);

            return new GeneratedSshKey
            {
                PrivateKeyPem = privatePem,
                PublicKey = publicKey,
                Fingerprint = fingerprint,
                Algorithm = request.Algorithm,
                Comment = comment,
                HasPassphrase = !string.IsNullOrEmpty(request.Passphrase)
            };
        }, cancellationToken);
    }

    public ImportedKey ParsePrivateKey(string pem, string? passphrase)
    {
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new ArgumentException("Private key content is required.", nameof(pem));
        }

        try
        {
            var normalized = NormalizePem(pem);
            var hasPassphrase = OpenSshPemCodec.IsEncrypted(normalized);
            if (hasPassphrase && string.IsNullOrEmpty(passphrase))
            {
                throw new InvalidDataException("Private key is encrypted. Enter the passphrase.");
            }

            var privateKey = OpenSshPemCodec.DecodePrivateKey(normalized, passphrase);
            var publicKeyParam = DerivePublicKey(privateKey);
            var algorithm = ResolveAlgorithm(privateKey);
            var comment = TryReadComment(normalized, passphrase) ?? string.Empty;
            var publicKeyLine = FormatPublicKey(publicKeyParam, comment);
            var fingerprint = ComputeFingerprint(publicKeyParam);

            return new ImportedKey
            {
                PrivateKeyPem = normalized,
                PublicKey = publicKeyLine,
                Fingerprint = fingerprint,
                Algorithm = algorithm,
                Comment = comment,
                HasPassphrase = hasPassphrase
            };
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("Failed to parse OpenSSH private key. Check format and passphrase.", ex);
        }
    }

    private static string EncodeWithComment(
        AsymmetricKeyParameter privateKey,
        string comment,
        string? passphrase)
    {
        var unencrypted = OpenSshPrivateKeyUtilities.EncodePrivateKey(privateKey);
        var withComment = InjectComment(unencrypted, comment);
        return OpenSshPemCodec.EncodeUnencryptedBlob(withComment, passphrase);
    }

    private static byte[] InjectComment(byte[] unencryptedBlob, string comment)
    {
        var reader = new SshBufferReader(unencryptedBlob);
        var magic = reader.ReadBytes(15);
        var cipher = reader.ReadUtf8String();
        var kdf = reader.ReadUtf8String();
        var kdfOpts = reader.ReadString();
        var nkeys = reader.ReadUInt32();
        var publicKey = reader.ReadString();
        var privateSection = reader.ReadString();

        var privReader = new SshBufferReader(privateSection);
        var check1 = privReader.ReadUInt32();
        var check2 = privReader.ReadUInt32();
        var keyType = privReader.ReadUtf8String();

        var rebuiltPriv = new SshBufferWriter();
        rebuiltPriv.WriteUInt32(check1);
        rebuiltPriv.WriteUInt32(check2);
        rebuiltPriv.WriteString(keyType);

        if (keyType == "ssh-ed25519")
        {
            rebuiltPriv.WriteString(privReader.ReadString());
            rebuiltPriv.WriteString(privReader.ReadString());
        }
        else if (keyType == "ssh-rsa")
        {
            for (var i = 0; i < 6; i++)
            {
                rebuiltPriv.WriteString(privReader.ReadString());
            }
        }
        else
        {
            return unencryptedBlob;
        }

        // Skip original comment
        _ = privReader.ReadString();
        rebuiltPriv.WriteString(comment ?? string.Empty);

        // OpenSSH padding: bytes 1..n until multiple of 8
        var body = rebuiltPriv.ToArray();
        var padLen = 8 - (body.Length % 8);
        if (padLen == 0)
        {
            padLen = 8;
        }

        var padded = new byte[body.Length + padLen];
        Buffer.BlockCopy(body, 0, padded, 0, body.Length);
        for (var i = 0; i < padLen; i++)
        {
            padded[body.Length + i] = (byte)(i + 1);
        }

        var writer = new SshBufferWriter();
        writer.WriteBytes(magic);
        writer.WriteString(cipher);
        writer.WriteString(kdf);
        writer.WriteString(kdfOpts);
        writer.WriteUInt32(nkeys);
        writer.WriteString(publicKey);
        writer.WriteString(padded);
        return writer.ToArray();
    }

    private static string? TryReadComment(string pem, string? passphrase)
    {
        try
        {
            var key = OpenSshPemCodec.DecodePrivateKey(pem, passphrase);
            var unencrypted = OpenSshPrivateKeyUtilities.EncodePrivateKey(key);
            // Comment is lost on BC re-encode; read from original decrypted blob instead.
            var blob = OpenSshPemCodec.UnwrapPem(pem);
            if (OpenSshPemCodec.IsEncrypted(pem))
            {
                return ReadCommentFromPrivateKeyParameter(pem, passphrase);
            }

            return ReadCommentFromUnencryptedBlob(blob);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadCommentFromPrivateKeyParameter(string pem, string? passphrase)
    {
        try
        {
            // Decrypt manually to preserve comment in private section.
            var blob = OpenSshPemCodec.UnwrapPem(pem);
            var reader = new SshBufferReader(blob);
            _ = reader.ReadBytes(15);
            var cipherName = reader.ReadUtf8String();
            var kdfName = reader.ReadUtf8String();
            var kdfOptions = reader.ReadString();
            _ = reader.ReadUInt32();
            _ = reader.ReadString();
            var privateSection = reader.ReadString();

            if (!string.Equals(cipherName, "none", StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(passphrase))
                {
                    return null;
                }

                var kdfReader = new SshBufferReader(kdfOptions);
                var salt = kdfReader.ReadString();
                var rounds = (int)kdfReader.ReadUInt32();
                var keyIv = new byte[48];
                new OpenSshBcryptPbkdf().DeriveKey(Encoding.UTF8.GetBytes(passphrase), salt, rounds, keyIv);
                var aesKey = keyIv.AsSpan(0, 32).ToArray();
                var iv = keyIv.AsSpan(32, 16).ToArray();
                privateSection = AesCtr(privateSection, aesKey, iv);
                CryptographicOperations.ZeroMemory(aesKey);
                CryptographicOperations.ZeroMemory(iv);
                CryptographicOperations.ZeroMemory(keyIv);
            }

            return ReadCommentFromPrivateSection(privateSection);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadCommentFromUnencryptedBlob(byte[] blob)
    {
        var reader = new SshBufferReader(blob);
        _ = reader.ReadBytes(15);
        _ = reader.ReadUtf8String();
        _ = reader.ReadUtf8String();
        _ = reader.ReadString();
        _ = reader.ReadUInt32();
        _ = reader.ReadString();
        return ReadCommentFromPrivateSection(reader.ReadString());
    }

    private static string? ReadCommentFromPrivateSection(byte[] privateSection)
    {
        var privReader = new SshBufferReader(privateSection);
        _ = privReader.ReadUInt32();
        _ = privReader.ReadUInt32();
        var keyType = privReader.ReadUtf8String();
        if (keyType == "ssh-ed25519")
        {
            _ = privReader.ReadString();
            _ = privReader.ReadString();
        }
        else if (keyType == "ssh-rsa")
        {
            for (var i = 0; i < 6; i++)
            {
                _ = privReader.ReadString();
            }
        }
        else
        {
            return null;
        }

        return privReader.ReadUtf8String();
    }

    private static byte[] AesCtr(byte[] input, byte[] key, byte[] iv)
    {
        var cipher = new StreamBlockCipher(new Org.BouncyCastle.Crypto.Modes.SicBlockCipher(new Org.BouncyCastle.Crypto.Engines.AesEngine()));
        cipher.Init(false, new ParametersWithIV(new KeyParameter(key), iv));
        var output = new byte[input.Length];
        cipher.ProcessBytes(input, 0, input.Length, output, 0);
        return output;
    }

    private static AsymmetricCipherKeyPair CreateKeyPair(SshKeyAlgorithm algorithm)
    {
        return algorithm switch
        {
            SshKeyAlgorithm.Ed25519 => CreateEd25519(),
            SshKeyAlgorithm.Rsa4096 => CreateRsa4096(),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported algorithm.")
        };
    }

    private static AsymmetricCipherKeyPair CreateEd25519()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        return generator.GenerateKeyPair();
    }

    private static AsymmetricCipherKeyPair CreateRsa4096()
    {
        var generator = new RsaKeyPairGenerator();
        generator.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(0x10001), new SecureRandom(), 4096, 100));
        return generator.GenerateKeyPair();
    }

    private static AsymmetricKeyParameter DerivePublicKey(AsymmetricKeyParameter privateKey)
    {
        return privateKey switch
        {
            Ed25519PrivateKeyParameters ed => ed.GeneratePublicKey(),
            RsaPrivateCrtKeyParameters rsa => new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent),
            _ => throw new NotSupportedException($"Unsupported private key type: {privateKey.GetType().Name}")
        };
    }

    private static SshKeyAlgorithm ResolveAlgorithm(AsymmetricKeyParameter privateKey)
    {
        return privateKey switch
        {
            Ed25519PrivateKeyParameters => SshKeyAlgorithm.Ed25519,
            RsaKeyParameters => SshKeyAlgorithm.Rsa4096,
            _ => throw new NotSupportedException($"Unsupported key type: {privateKey.GetType().Name}")
        };
    }

    private static string FormatPublicKey(AsymmetricKeyParameter publicKey, string comment)
    {
        var blob = OpenSshPublicKeyUtilities.EncodePublicKey(publicKey);
        var alg = publicKey switch
        {
            Ed25519PublicKeyParameters => "ssh-ed25519",
            RsaKeyParameters => "ssh-rsa",
            _ => throw new NotSupportedException($"Unsupported public key type: {publicKey.GetType().Name}")
        };

        var line = $"{alg} {Convert.ToBase64String(blob)}";
        if (!string.IsNullOrWhiteSpace(comment))
        {
            line += " " + comment.Trim();
        }

        return line;
    }

    private static string ComputeFingerprint(AsymmetricKeyParameter publicKey)
    {
        var blob = OpenSshPublicKeyUtilities.EncodePublicKey(publicKey);
        var hash = SHA256.HashData(blob);
        return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
    }

    private static string BuildDefaultComment()
    {
        var user = string.IsNullOrWhiteSpace(Environment.UserName) ? "user" : Environment.UserName;
        var host = string.IsNullOrWhiteSpace(Environment.MachineName) ? "host" : Environment.MachineName;
        return $"{user}@{host}";
    }

    private static string NormalizePem(string pem) =>
        pem.Replace("\r\n", "\n", StringComparison.Ordinal).Trim() + "\n";
}
