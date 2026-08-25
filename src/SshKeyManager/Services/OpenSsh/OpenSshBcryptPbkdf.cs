using System.Security.Cryptography;
using System.Text;

namespace SshKeyManager.Services.OpenSsh;

/// <summary>
/// OpenSSH bcrypt_pbkdf (PROTOCOL.key / OpenBSD bcrypt_pbkdf).
/// </summary>
internal sealed class OpenSshBcryptPbkdf
{
    private readonly uint[] _p = new uint[18];
    private readonly uint[] _s = new uint[1024];

    public void DeriveKey(byte[] password, byte[] salt, int rounds, byte[] output)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(output);
        if (rounds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rounds));
        }

        var nblocks = (output.Length + 31) / 32;
        var hpass = SHA512.HashData(password);
        try
        {
            for (var block = 1; block <= nblocks; block++)
            {
                var blockBe = new byte[]
                {
                    (byte)((block >> 24) & 0xff),
                    (byte)((block >> 16) & 0xff),
                    (byte)((block >> 8) & 0xff),
                    (byte)(block & 0xff)
                };

                var hsalt = SHA512.HashData(Concat(salt, blockBe));
                var outBlock = new byte[32];
                Hash(hpass, hsalt, outBlock);
                var tmp = (byte[])outBlock.Clone();

                for (var round = 1; round < rounds; round++)
                {
                    hsalt = SHA512.HashData(tmp);
                    Hash(hpass, hsalt, tmp);
                    for (var i = 0; i < 32; i++)
                    {
                        outBlock[i] ^= tmp[i];
                    }
                }

                for (var i = 0; i < 32; i++)
                {
                    var idx = i * nblocks + (block - 1);
                    if (idx < output.Length)
                    {
                        output[idx] = outBlock[i];
                    }
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hpass);
        }
    }

    private void Hash(byte[] sha2Pass, byte[] sha2Salt, byte[] output)
    {
        InitState();
        ExpandState(sha2Pass, sha2Salt);

        for (var i = 0; i < 64; i++)
        {
            Expand0State(sha2Salt);
            Expand0State(sha2Pass);
        }

        var cdata = Encoding.ASCII.GetBytes("OxychromaticBlowfishSwatDynamite");
        for (var i = 0; i < 64; i++)
        {
            for (var j = 0; j < 8; j++)
            {
                Encipher(cdata, j * 8);
            }
        }

        for (var i = 0; i < 32; i += 4)
        {
            output[i] = cdata[i + 3];
            output[i + 1] = cdata[i + 2];
            output[i + 2] = cdata[i + 1];
            output[i + 3] = cdata[i];
        }
    }

    private void InitState()
    {
        Array.Copy(BlowfishDefaults.P, _p, 18);
        Array.Copy(BlowfishDefaults.S, _s, 1024);
    }

    private void ExpandState(byte[] key, byte[] data)
    {
        var lr = new uint[2];
        var keyIndex = 0;
        for (var i = 0; i < 18; i++)
        {
            _p[i] ^= Stream2Word(key, ref keyIndex);
        }

        var dataIndex = 0;
        for (var i = 0; i < 18; i += 2)
        {
            lr[0] ^= Stream2Word(data, ref dataIndex);
            lr[1] ^= Stream2Word(data, ref dataIndex);
            Encipher(lr);
            _p[i] = lr[0];
            _p[i + 1] = lr[1];
        }

        for (var i = 0; i < 1024; i += 2)
        {
            lr[0] ^= Stream2Word(data, ref dataIndex);
            lr[1] ^= Stream2Word(data, ref dataIndex);
            Encipher(lr);
            _s[i] = lr[0];
            _s[i + 1] = lr[1];
        }
    }

    private void Expand0State(byte[] key)
    {
        var lr = new uint[2];
        var keyIndex = 0;
        for (var i = 0; i < 18; i++)
        {
            _p[i] ^= Stream2Word(key, ref keyIndex);
        }

        for (var i = 0; i < 18; i += 2)
        {
            Encipher(lr);
            _p[i] = lr[0];
            _p[i + 1] = lr[1];
        }

        for (var i = 0; i < 1024; i += 2)
        {
            Encipher(lr);
            _s[i] = lr[0];
            _s[i + 1] = lr[1];
        }
    }

    private void Encipher(byte[] data, int offset)
    {
        var lr = new uint[2];
        lr[0] = LoadBe(data, offset);
        lr[1] = LoadBe(data, offset + 4);
        Encipher(lr);
        StoreBe(data, offset, lr[0]);
        StoreBe(data, offset + 4, lr[1]);
    }

    private void Encipher(uint[] lr)
    {
        uint l = lr[0];
        uint r = lr[1];
        l ^= _p[0];
        for (var i = 0; i < 16;)
        {
            r ^= F(l) ^ _p[++i];
            l ^= F(r) ^ _p[++i];
        }

        lr[0] = r ^ _p[17];
        lr[1] = l;
    }

    private uint F(uint x) =>
        ((_s[(x >> 24) & 0xff] + _s[0x100 | ((x >> 16) & 0xff)]) ^ _s[0x200 | ((x >> 8) & 0xff)]) +
        _s[0x300 | (x & 0xff)];

    private static uint Stream2Word(byte[] data, ref int offset)
    {
        uint word = 0;
        for (var i = 0; i < 4; i++)
        {
            word = (word << 8) | data[offset];
            offset = (offset + 1) % data.Length;
        }

        return word;
    }

    private static uint LoadBe(byte[] data, int offset) =>
        ((uint)data[offset] << 24) |
        ((uint)data[offset + 1] << 16) |
        ((uint)data[offset + 2] << 8) |
        data[offset + 3];

    private static void StoreBe(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var result = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, result, 0, a.Length);
        Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
        return result;
    }
}
