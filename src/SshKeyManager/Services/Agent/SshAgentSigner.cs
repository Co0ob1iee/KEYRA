using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using SshKeyManager.Services.OpenSsh;

namespace SshKeyManager.Services.Agent;

/// <summary>
/// Builds OpenSSH agent signature blobs from vault OpenSSH private key PEMs.
/// Unsupported key types throw <see cref="NotSupportedException"/> (caller maps to SSH_AGENT_FAILURE).
/// </summary>
internal static class SshAgentSigner
{
    private const uint AgentRsaSha2_256 = 2;
    private const uint AgentRsaSha2_512 = 4;

    /// <summary>
    /// Returns the wire-format signature string payload for SSH_AGENT_SIGN_RESPONSE
    /// (algorithm name + signature blob), not including the outer agent message type.
    /// </summary>
    public static byte[] Sign(
        string privateKeyPem,
        string? passphrase,
        ReadOnlySpan<byte> data,
        uint flags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);

        AsymmetricKeyParameter privateKey;
        try
        {
            privateKey = OpenSshPemCodec.DecodePrivateKey(privateKeyPem, passphrase);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or CryptographicException)
        {
            throw new NotSupportedException("Unable to load private key for agent signing.", ex);
        }

        return privateKey switch
        {
            Ed25519PrivateKeyParameters ed => WrapSignature("ssh-ed25519", SignEd25519(ed, data)),
            RsaPrivateCrtKeyParameters rsa => SignRsa(rsa, data, flags),
            ECPrivateKeyParameters ec => SignEcdsa(ec, data),
            _ => throw new NotSupportedException($"Agent signing is not supported for {privateKey.GetType().Name}.")
        };
    }

    private static byte[] SignEd25519(Ed25519PrivateKeyParameters key, ReadOnlySpan<byte> data)
    {
        var buf = data.ToArray();
        try
        {
            var signer = new Ed25519Signer();
            signer.Init(true, key);
            signer.BlockUpdate(buf, 0, buf.Length);
            return signer.GenerateSignature();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buf);
        }
    }

    private static byte[] SignRsa(RsaPrivateCrtKeyParameters key, ReadOnlySpan<byte> data, uint flags)
    {
        string algorithm;
        string signerName;
        if ((flags & AgentRsaSha2_512) != 0)
        {
            algorithm = "rsa-sha2-512";
            signerName = "SHA-512withRSA";
        }
        else if ((flags & AgentRsaSha2_256) != 0)
        {
            algorithm = "rsa-sha2-256";
            signerName = "SHA-256withRSA";
        }
        else
        {
            // Match classic OpenSSH agent when no SHA-2 flags are set.
            algorithm = "ssh-rsa";
            signerName = "SHA-1withRSA";
        }

        var buf = data.ToArray();
        try
        {
            var signer = SignerUtilities.GetSigner(signerName);
            signer.Init(true, key);
            signer.BlockUpdate(buf, 0, buf.Length);
            return WrapSignature(algorithm, signer.GenerateSignature());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buf);
        }
    }

    private static byte[] SignEcdsa(ECPrivateKeyParameters key, ReadOnlySpan<byte> data)
    {
        var curveName = ResolveEcdsaCurveName(key);
        var hashName = curveName switch
        {
            "nistp256" => "SHA-256withECDSA",
            "nistp384" => "SHA-384withECDSA",
            "nistp521" => "SHA-512withECDSA",
            _ => throw new NotSupportedException($"ECDSA curve '{curveName}' is not supported for agent signing.")
        };

        var buf = data.ToArray();
        try
        {
            var signer = SignerUtilities.GetSigner(hashName);
            signer.Init(true, key);
            signer.BlockUpdate(buf, 0, buf.Length);
            var der = signer.GenerateSignature();
            var sshBlob = DerEcdsaToSshBlob(der);
            return WrapSignature("ecdsa-sha2-" + curveName, sshBlob);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buf);
        }
    }

    private static string ResolveEcdsaCurveName(ECPrivateKeyParameters key)
    {
        var p256 = SecNamedCurves.GetByOid(SecObjectIdentifiers.SecP256r1);
        var p384 = SecNamedCurves.GetByOid(SecObjectIdentifiers.SecP384r1);
        var p521 = SecNamedCurves.GetByOid(SecObjectIdentifiers.SecP521r1);
        if (key.Parameters.N.Equals(p256.N))
        {
            return "nistp256";
        }

        if (key.Parameters.N.Equals(p384.N))
        {
            return "nistp384";
        }

        if (key.Parameters.N.Equals(p521.N))
        {
            return "nistp521";
        }

        throw new NotSupportedException("Unsupported ECDSA curve for agent signing.");
    }

    private static byte[] DerEcdsaToSshBlob(byte[] derSignature)
    {
        var seq = Asn1Sequence.GetInstance(derSignature);
        if (seq.Count != 2)
        {
            throw new InvalidDataException("Unexpected ECDSA signature encoding.");
        }

        var r = DerInteger.GetInstance(seq[0]).Value;
        var s = DerInteger.GetInstance(seq[1]).Value;
        using var ms = new MemoryStream();
        WriteMpint(ms, r);
        WriteMpint(ms, s);
        return ms.ToArray();
    }

    private static void WriteMpint(Stream stream, BigInteger value)
    {
        var bytes = value.ToByteArray();
        // OpenSSH mpint: two's-complement big-endian without unnecessary leading zeros,
        // except a leading zero byte when the high bit would otherwise make it negative.
        WriteString(stream, bytes);
    }

    private static byte[] WrapSignature(string algorithm, byte[] signatureBlob)
    {
        using var ms = new MemoryStream();
        WriteString(ms, Encoding.UTF8.GetBytes(algorithm));
        WriteString(ms, signatureBlob);
        return ms.ToArray();
    }

    private static void WriteString(Stream stream, ReadOnlySpan<byte> value)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, (uint)value.Length);
        stream.Write(len);
        stream.Write(value);
    }
}
