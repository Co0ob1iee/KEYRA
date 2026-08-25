using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace SshKeyManager.Services.OpenSsh;

internal sealed class SshBufferWriter
{
    private readonly MemoryStream _stream = new();

    public void WriteUInt32(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteString(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteUInt32((uint)value.Length);
        _stream.Write(value, 0, value.Length);
    }

    public void WriteString(string value, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        WriteString(encoding.GetBytes(value ?? string.Empty));
    }

    public void WriteBytes(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _stream.Write(value, 0, value.Length);
    }

    public byte[] ToArray() => _stream.ToArray();
}

internal sealed class SshBufferReader
{
    private readonly byte[] _data;
    private int _offset;

    public SshBufferReader(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public int Remaining => _data.Length - _offset;

    public uint ReadUInt32()
    {
        if (Remaining < 4)
        {
            throw new InvalidDataException("Unexpected end of SSH buffer.");
        }

        var value = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(_offset, 4));
        _offset += 4;
        return value;
    }

    public byte[] ReadBytes(int length)
    {
        if (length < 0 || Remaining < length)
        {
            throw new InvalidDataException("Unexpected end of SSH buffer.");
        }

        var result = new byte[length];
        Buffer.BlockCopy(_data, _offset, result, 0, length);
        _offset += length;
        return result;
    }

    public byte[] ReadString()
    {
        var length = (int)ReadUInt32();
        return ReadBytes(length);
    }

    public string ReadUtf8String() => Encoding.UTF8.GetString(ReadString());
}
