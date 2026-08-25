using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SshKeyManager.Services.Security;

/// <summary>
/// Locked RAM helpers: VirtualLock to reduce pagefile exposure and memzero on dispose.
/// </summary>
public sealed class SecureBuffer : IDisposable
{
    private GCHandle _handle;
    private byte[]? _data;
    private bool _locked;
    private bool _disposed;

    public SecureBuffer(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _data = new byte[length];
        _handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
        try
        {
            _locked = NativeMethods.VirtualLock(_handle.AddrOfPinnedObject(), (UIntPtr)(nuint)length);
        }
        catch
        {
            _locked = false;
        }
    }

    public SecureBuffer(ReadOnlySpan<byte> source)
        : this(source.Length)
    {
        source.CopyTo(Span);
    }

    public int Length => _data?.Length ?? 0;

    public Span<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _data!.AsSpan();
        }
    }

    public byte[] DangerousGetArray()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _data!;
    }

    public byte[] ToArrayCopy()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _data!.ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_data is not null)
        {
            if (_locked && _handle.IsAllocated)
            {
                try
                {
                    NativeMethods.VirtualUnlock(_handle.AddrOfPinnedObject(), (UIntPtr)(nuint)_data.Length);
                }
                catch
                {
                    // Best-effort unlock.
                }
            }

            CryptographicOperations.ZeroMemory(_data);
            _data = null;
        }

        if (_handle.IsAllocated)
        {
            _handle.Free();
        }

        GC.SuppressFinalize(this);
    }

    ~SecureBuffer()
    {
        Dispose();
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualLock(IntPtr lpAddress, UIntPtr dwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualUnlock(IntPtr lpAddress, UIntPtr dwSize);
    }
}

internal static class SecureMemory
{
    public static void Memzero(byte[]? buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(buffer);
    }

    public static void Memzero(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(buffer);
    }

    public static void Memzero(char[]? buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        Array.Clear(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Best-effort wipe of a temporary UTF-8 encoding of a secret string.
    /// The immutable <see cref="string"/> itself cannot be cleared.
    /// </summary>
    public static void MemzeroUtf8Copy(ref string? secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            secret = null;
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(secret);
        CryptographicOperations.ZeroMemory(bytes);
        secret = null;
    }
}
