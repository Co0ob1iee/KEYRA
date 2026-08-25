using System.Text;

namespace SshKeyManager.Services.Security;

public sealed class MasterKeyFileData
{
    public string Username { get; init; } = string.Empty;

    public byte[] PasswordVerifierSalt { get; init; } = [];

    public byte[] PasswordVerifierHash { get; init; } = [];

    public byte[] KekSalt { get; init; } = [];

    public byte[] EncryptedMasterKey { get; init; } = [];
}

public static class MasterKeyFileFormat
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SKM1");
    private const byte Version = 1;

    public static byte[] Serialize(MasterKeyFileData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var ms = new MemoryStream();
        ms.Write(Magic, 0, Magic.Length);
        ms.WriteByte(Version);

        WriteString(ms, data.Username);
        WriteBytes(ms, data.PasswordVerifierSalt);
        WriteBytes(ms, data.PasswordVerifierHash);
        WriteBytes(ms, data.KekSalt);
        WriteBytes(ms, data.EncryptedMasterKey);
        return ms.ToArray();
    }

    public static MasterKeyFileData Deserialize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Magic.Length + 1)
        {
            throw new InvalidDataException("Master key file is too short.");
        }

        if (!bytes[..Magic.Length].SequenceEqual(Magic))
        {
            throw new InvalidDataException("Invalid master key file signature.");
        }

        if (bytes[Magic.Length] != Version)
        {
            throw new InvalidDataException("Unsupported master key file version.");
        }

        var offset = Magic.Length + 1;
        var username = ReadString(bytes, ref offset);
        var passwordSalt = ReadBytes(bytes, ref offset);
        var passwordHash = ReadBytes(bytes, ref offset);
        var kekSalt = ReadBytes(bytes, ref offset);
        var encryptedMaster = ReadBytes(bytes, ref offset);

        if (offset != bytes.Length)
        {
            throw new InvalidDataException("Master key file contains trailing data.");
        }

        return new MasterKeyFileData
        {
            Username = username,
            PasswordVerifierSalt = passwordSalt,
            PasswordVerifierHash = passwordHash,
            KekSalt = kekSalt,
            EncryptedMasterKey = encryptedMaster
        };
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("Username is too long.");
        }

        stream.WriteByte((byte)(bytes.Length >> 8));
        stream.WriteByte((byte)bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteBytes(Stream stream, byte[] value)
    {
        if (value.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("Field is too long.");
        }

        stream.WriteByte((byte)(value.Length >> 8));
        stream.WriteByte((byte)value.Length);
        stream.Write(value, 0, value.Length);
    }

    private static string ReadString(ReadOnlySpan<byte> bytes, ref int offset)
    {
        var length = ReadLength(bytes, ref offset);
        var text = Encoding.UTF8.GetString(bytes.Slice(offset, length));
        offset += length;
        return text;
    }

    private static byte[] ReadBytes(ReadOnlySpan<byte> bytes, ref int offset)
    {
        var length = ReadLength(bytes, ref offset);
        var result = bytes.Slice(offset, length).ToArray();
        offset += length;
        return result;
    }

    private static int ReadLength(ReadOnlySpan<byte> bytes, ref int offset)
    {
        if (offset + 2 > bytes.Length)
        {
            throw new InvalidDataException("Unexpected end of master key file.");
        }

        var length = (bytes[offset] << 8) | bytes[offset + 1];
        offset += 2;
        if (offset + length > bytes.Length)
        {
            throw new InvalidDataException("Invalid field length in master key file.");
        }

        return length;
    }
}
