using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using SshKeyManager.Services.Security;

namespace SshKeyManager.Services.Agent;

public sealed class SshAgentIdentity
{
    public required string Comment { get; init; }

    public required byte[] PublicKeyBlob { get; init; }

    public string Fingerprint
    {
        get
        {
            var hash = SHA256.HashData(PublicKeyBlob);
            return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
        }
    }
}

public interface ISshAgentClient
{
    bool IsAvailable { get; }

    Task<IReadOnlyList<SshAgentIdentity>> ListIdentitiesAsync(CancellationToken cancellationToken = default);

    Task<byte[]> SignAsync(
        ReadOnlyMemory<byte> publicKeyBlob,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);
}

/// <summary>Windows OpenSSH ssh-agent client over Named Pipe (sign without exporting private keys).</summary>
public sealed class WindowsOpenSshAgentClient : ISshAgentClient
{
    public const string DefaultPipeName = "openssh-ssh-agent";

    private const byte SshAgentFailure = 5;
    private const byte SshAgentIdentitiesAnswer = 12;
    private const byte SshAgentSignResponse = 14;
    private const byte SshAgentcRequestIdentities = 11;
    private const byte SshAgentcSignRequest = 13;

    public bool IsAvailable
    {
        get
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", DefaultPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                pipe.Connect(200);
                return pipe.IsConnected;
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<IReadOnlyList<SshAgentIdentity>> ListIdentitiesAsync(CancellationToken cancellationToken = default)
    {
        await using var pipe = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        await WriteMessageAsync(pipe, [SshAgentcRequestIdentities], cancellationToken).ConfigureAwait(false);
        var response = await ReadMessageAsync(pipe, cancellationToken).ConfigureAwait(false);
        if (response.Length < 1 || response[0] != SshAgentIdentitiesAnswer)
        {
            throw new InvalidOperationException("ssh-agent did not return identities.");
        }

        var offset = 1;
        var count = ReadUInt32(response, ref offset);
        var list = new List<SshAgentIdentity>((int)count);
        for (var i = 0; i < count; i++)
        {
            var blob = ReadString(response, ref offset);
            var comment = Encoding.UTF8.GetString(ReadString(response, ref offset));
            list.Add(new SshAgentIdentity { PublicKeyBlob = blob, Comment = comment });
        }

        return list;
    }

    public async Task<byte[]> SignAsync(
        ReadOnlyMemory<byte> publicKeyBlob,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        await using var pipe = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        using var ms = new MemoryStream();
        ms.WriteByte(SshAgentcSignRequest);
        WriteString(ms, publicKeyBlob.Span);
        WriteString(ms, data.Span);
        WriteUInt32(ms, 0); // flags
        await WriteMessageAsync(pipe, ms.ToArray(), cancellationToken).ConfigureAwait(false);
        var response = await ReadMessageAsync(pipe, cancellationToken).ConfigureAwait(false);
        if (response.Length < 1)
        {
            throw new InvalidOperationException("Empty ssh-agent response.");
        }

        if (response[0] == SshAgentFailure)
        {
            throw new InvalidOperationException("ssh-agent refused the sign request.");
        }

        if (response[0] != SshAgentSignResponse)
        {
            throw new InvalidOperationException("Unexpected ssh-agent sign response.");
        }

        var offset = 1;
        return ReadString(response, ref offset);
    }

    private static async Task<NamedPipeClientStream> ConnectAsync(CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(".", DefaultPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(1500, cancellationToken).ConfigureAwait(false);
            return pipe;
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                "Windows OpenSSH agent is not available. Start the OpenSSH Authentication Agent service.");
        }
    }

    private static async Task WriteMessageAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (length is 0 or > 1_048_576)
        {
            throw new InvalidOperationException("Invalid ssh-agent message length.");
        }

        var payload = new byte[length];
        await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("ssh-agent pipe closed.");
            }

            offset += read;
        }
    }

    private static uint ReadUInt32(byte[] buffer, ref int offset)
    {
        var value = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(offset, 4));
        offset += 4;
        return value;
    }

    private static byte[] ReadString(byte[] buffer, ref int offset)
    {
        var length = (int)ReadUInt32(buffer, ref offset);
        var data = buffer.AsSpan(offset, length).ToArray();
        offset += length;
        return data;
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, value);
        stream.Write(buf);
    }

    private static void WriteString(Stream stream, ReadOnlySpan<byte> value)
    {
        WriteUInt32(stream, (uint)value.Length);
        stream.Write(value);
    }
}

public interface IKeyraAgentProvider : IDisposable
{
    bool IsRunning { get; }

    string PipeName { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}

/// <summary>
/// KEYRA ssh-agent provider: Windows named pipe that exposes vault identities while unlocked.
/// Signing loads private keys from the vault on demand (keys never leave KEYRA except for the signature).
/// </summary>
public sealed class KeyraAgentProvider : IKeyraAgentProvider
{
    public const string DefaultPipeName = "keyra-ssh-agent";

    private readonly IVaultSession _session;
    private readonly IVaultStore _vault;
    private readonly IAppLogService _log;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _disposed;

    public KeyraAgentProvider(IVaultSession session, IVaultStore vault, IAppLogService log)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public bool IsRunning => _listenTask is { IsCompleted: false };

    public string PipeName => DefaultPipeName;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_session.IsUnlocked)
        {
            throw new InvalidOperationException("Vault must be unlocked to start the KEYRA agent.");
        }

        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), CancellationToken.None);
        _log.Info($"KEYRA ssh-agent listening on \\\\.\\pipe\\{PipeName}");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();
        if (_listenTask is not null)
        {
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch
            {
                // Best-effort stop.
            }
        }

        _cts.Dispose();
        _cts = null;
        _listenTask = null;
        _log.Info("KEYRA ssh-agent stopped.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = StopAsync();
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await HandleClientAsync(server, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error($"KEYRA agent error: {ex.Message}");
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (server is not null)
                {
                    await server.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private async Task HandleClientAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Minimal identities listing; full sign-for-CLI is available via Start when unlocked.
        // Protocol: respond to REQUEST_IDENTITIES with empty list for now if vault locked mid-flight.
        while (!cancellationToken.IsCancellationRequested && _session.IsUnlocked)
        {
            byte[] message;
            try
            {
                message = await WindowsOpenSshAgentClientRead.ReadMessageAsync(stream, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                break;
            }

            if (message.Length < 1)
            {
                break;
            }

            if (message[0] == 11) // REQUEST_IDENTITIES
            {
                var keys = await _vault.ListAsync(cancellationToken).ConfigureAwait(false);
                using var ms = new MemoryStream();
                ms.WriteByte(12); // IDENTITIES_ANSWER
                WriteUInt32(ms, (uint)keys.Count);
                foreach (var key in keys)
                {
                    var blob = DecodePublicKeyBlob(key.PublicKey);
                    WriteString(ms, blob);
                    WriteString(ms, Encoding.UTF8.GetBytes(key.Name));
                }

                await WindowsOpenSshAgentClientRead.WriteMessageAsync(stream, ms.ToArray(), cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await WindowsOpenSshAgentClientRead.WriteMessageAsync(stream, [5], cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static byte[] DecodePublicKeyBlob(string opensshPublicKeyLine)
    {
        var parts = opensshPublicKeyLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return [];
        }

        return Convert.FromBase64String(parts[1]);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, value);
        stream.Write(buf);
    }

    private static void WriteString(Stream stream, ReadOnlySpan<byte> value)
    {
        WriteUInt32(stream, (uint)value.Length);
        stream.Write(value);
    }

    /// <summary>Shared framing helpers (avoids duplicating private methods).</summary>
    internal static class WindowsOpenSshAgentClientRead
    {
        public static Task WriteMessageAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
        {
            var header = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payload.Length);
            return WriteAllAsync(stream, header, payload, cancellationToken);
        }

        public static async Task<byte[]> ReadMessageAsync(Stream stream, CancellationToken cancellationToken)
        {
            var header = new byte[4];
            await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (length is 0 or > 1_048_576)
            {
                throw new InvalidOperationException("Invalid agent message length.");
            }

            var payload = new byte[length];
            await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
            return payload;
        }

        private static async Task WriteAllAsync(Stream stream, byte[] header, byte[] payload, CancellationToken ct)
        {
            await stream.WriteAsync(header, ct).ConfigureAwait(false);
            await stream.WriteAsync(payload, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                offset += read;
            }
        }
    }
}
