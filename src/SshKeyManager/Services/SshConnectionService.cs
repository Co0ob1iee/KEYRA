using System.Text;
using Renci.SshNet;

namespace SshKeyManager.Services;

public enum SshConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting
}

public sealed class SshConnectionEventArgs : EventArgs
{
    public SshConnectionEventArgs(string text, bool isError = false)
    {
        Text = text;
        IsError = isError;
    }

    public string Text { get; }

    public bool IsError { get; }
}

public interface ISshConnectionService : IDisposable
{
    SshConnectionState State { get; }

    bool IsConnected { get; }

    event EventHandler<SshConnectionEventArgs>? OutputReceived;

    event EventHandler<SshConnectionState>? StateChanged;

    Task ConnectWithKeyAsync(
        string host,
        int port,
        string username,
        string privateKeyPem,
        string? keyPassphrase,
        CancellationToken cancellationToken = default);

    Task ConnectWithPasswordAsync(
        string host,
        int port,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task ConnectViaJumpHostAsync(
        string jumpHost,
        int jumpPort,
        string jumpUsername,
        string jumpPrivateKeyPem,
        string? jumpKeyPassphrase,
        string targetHost,
        int targetPort,
        string targetUsername,
        bool usePasswordAuth,
        string? targetPassword,
        string? targetPrivateKeyPem,
        string? targetKeyPassphrase,
        CancellationToken cancellationToken = default);

    Task SendCommandAsync(string command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> RequestTabCompletionAsync(
        string lineBeforeCursor,
        int cursorPosition,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}

public sealed class SshConnectionService : ISshConnectionService
{
    private const string CompletionStartMarker = "__AICOMPL_START__";
    private const string CompletionEndMarker = "__AICOMPL_END__";

    private readonly object _sync = new();
    private readonly object _completionSync = new();
    private readonly StringBuilder _completionBuffer = new();
    private SshClient? _client;
    private SshClient? _jumpClient;
    private ForwardedPortLocal? _jumpForward;
    private ShellStream? _shell;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private SshConnectionState _state = SshConnectionState.Disconnected;
    private volatile bool _completionCaptureActive;
    private TaskCompletionSource<string>? _completionTcs;

    public SshConnectionState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
        private set
        {
            lock (_sync)
            {
                if (_state == value)
                {
                    return;
                }

                _state = value;
            }

            StateChanged?.Invoke(this, value);
        }
    }

    public bool IsConnected
    {
        get
        {
            lock (_sync)
            {
                return _client?.IsConnected == true;
            }
        }
    }

    public event EventHandler<SshConnectionEventArgs>? OutputReceived;

    public event EventHandler<SshConnectionState>? StateChanged;

    public async Task ConnectWithKeyAsync(
        string host,
        int port,
        string username,
        string privateKeyPem,
        string? keyPassphrase,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);

        await DisconnectInternalAsync().ConfigureAwait(false);
        State = SshConnectionState.Connecting;

        try
        {
            await using var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(privateKeyPem));
            var keyFile = string.IsNullOrEmpty(keyPassphrase)
                ? new PrivateKeyFile(keyStream)
                : new PrivateKeyFile(keyStream, keyPassphrase);

            var connectionInfo = new PrivateKeyConnectionInfo(host, port, username, keyFile);
            await ConnectInternalAsync(connectionInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            State = SshConnectionState.Disconnected;
            RaiseOutput($"Connection failed: {ex.Message}", isError: true);
            throw new InvalidOperationException($"SSH connection to {host}:{port} failed.", ex);
        }
    }

    public async Task ConnectWithPasswordAsync(
        string host,
        int port,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        await DisconnectInternalAsync().ConfigureAwait(false);
        State = SshConnectionState.Connecting;

        try
        {
            var connectionInfo = new PasswordConnectionInfo(host, port, username, password);
            await ConnectInternalAsync(connectionInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            State = SshConnectionState.Disconnected;
            RaiseOutput($"Connection failed: {ex.Message}", isError: true);
            throw new InvalidOperationException($"SSH connection to {host}:{port} failed.", ex);
        }
    }

    public async Task ConnectViaJumpHostAsync(
        string jumpHost,
        int jumpPort,
        string jumpUsername,
        string jumpPrivateKeyPem,
        string? jumpKeyPassphrase,
        string targetHost,
        int targetPort,
        string targetUsername,
        bool usePasswordAuth,
        string? targetPassword,
        string? targetPrivateKeyPem,
        string? targetKeyPassphrase,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jumpHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(jumpUsername);
        ArgumentException.ThrowIfNullOrWhiteSpace(jumpPrivateKeyPem);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUsername);

        if (usePasswordAuth)
        {
            ArgumentNullException.ThrowIfNull(targetPassword);
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(targetPrivateKeyPem);
        }

        await DisconnectInternalAsync().ConfigureAwait(false);
        State = SshConnectionState.Connecting;

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var jumpKeyStream = new MemoryStream(Encoding.UTF8.GetBytes(jumpPrivateKeyPem));
                var jumpKeyFile = string.IsNullOrEmpty(jumpKeyPassphrase)
                    ? new PrivateKeyFile(jumpKeyStream)
                    : new PrivateKeyFile(jumpKeyStream, jumpKeyPassphrase);

                var jumpClient = new SshClient(jumpHost, jumpPort, jumpUsername, jumpKeyFile)
                {
                    ConnectionInfo = { Timeout = TimeSpan.FromSeconds(20) }
                };
                jumpClient.Connect();
                cancellationToken.ThrowIfCancellationRequested();

                // Bastion opens direct-tcpip to the target; KEYRA authenticates end-to-end on Key B.
                var forward = new ForwardedPortLocal("127.0.0.1", 0, targetHost, (uint)targetPort);
                jumpClient.AddForwardedPort(forward);
                forward.Start();
                cancellationToken.ThrowIfCancellationRequested();

                ConnectionInfo targetInfo;
                if (usePasswordAuth)
                {
                    targetInfo = new PasswordConnectionInfo(
                        "127.0.0.1",
                        (int)forward.BoundPort,
                        targetUsername,
                        targetPassword!);
                }
                else
                {
                    using var targetKeyStream = new MemoryStream(Encoding.UTF8.GetBytes(targetPrivateKeyPem!));
                    var targetKeyFile = string.IsNullOrEmpty(targetKeyPassphrase)
                        ? new PrivateKeyFile(targetKeyStream)
                        : new PrivateKeyFile(targetKeyStream, targetKeyPassphrase);
                    targetInfo = new PrivateKeyConnectionInfo(
                        "127.0.0.1",
                        (int)forward.BoundPort,
                        targetUsername,
                        targetKeyFile);
                }

                targetInfo.Timeout = TimeSpan.FromSeconds(20);
                var targetClient = new SshClient(targetInfo);
                targetClient.Connect();

                var shell = targetClient.CreateShellStream("xterm", 120, 40, 800, 600, 8192);
                lock (_sync)
                {
                    _jumpClient = jumpClient;
                    _jumpForward = forward;
                    _client = targetClient;
                    _shell = shell;
                }
            }, cancellationToken).ConfigureAwait(false);

            ShellStream? shell;
            lock (_sync)
            {
                shell = _shell;
            }

            if (shell is null)
            {
                throw new InvalidOperationException("Jump host tunnel did not establish a shell.");
            }

            _readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _readTask = Task.Run(() => ReadShellLoopAsync(shell, _readCts.Token), CancellationToken.None);

            State = SshConnectionState.Connected;
            RaiseOutput(
                $"Connected to {targetHost}:{targetPort} as {targetUsername} via jump {jumpHost}:{jumpPort}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await CleanupJumpAsync().ConfigureAwait(false);
            State = SshConnectionState.Disconnected;
            RaiseOutput($"Jump host connection failed: {ex.Message}", isError: true);
            throw new InvalidOperationException(
                $"SSH jump connection to {targetHost}:{targetPort} via {jumpHost}:{jumpPort} failed.",
                ex);
        }
    }

    public async Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        ShellStream? shell;
        lock (_sync)
        {
            shell = _shell;
        }

        if (shell is null || !IsConnected)
        {
            throw new InvalidOperationException("Not connected to an SSH session.");
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var line = command.EndsWith("\n", StringComparison.Ordinal) ? command : command + "\n";
        var bytes = Encoding.UTF8.GetBytes(line);
        await shell.WriteAsync(bytes.AsMemory(0, bytes.Length), cancellationToken).ConfigureAwait(false);
        await shell.FlushAsync(cancellationToken).ConfigureAwait(false);
        RaiseOutput($"> {command.TrimEnd('\r', '\n')}");
    }

    public async Task<IReadOnlyList<string>> RequestTabCompletionAsync(
        string lineBeforeCursor,
        int cursorPosition,
        CancellationToken cancellationToken = default)
    {
        ShellStream? shell;
        lock (_sync)
        {
            shell = _shell;
        }

        if (shell is null || !IsConnected)
        {
            throw new InvalidOperationException("Not connected to an SSH session.");
        }

        var script = BuildCompletionScript(lineBeforeCursor, cursorPosition);
        var bytes = Encoding.UTF8.GetBytes(script);

        TaskCompletionSource<string> tcs;
        lock (_completionSync)
        {
            _completionBuffer.Clear();
            tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completionTcs = tcs;
            _completionCaptureActive = true;
        }

        try
        {
            await shell.WriteAsync(bytes.AsMemory(0, bytes.Length), cancellationToken).ConfigureAwait(false);
            await shell.FlushAsync(cancellationToken).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

            var completedTask = await Task.WhenAny(
                tcs.Task,
                Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token)).ConfigureAwait(false);

            if (completedTask != tcs.Task)
            {
                throw new TimeoutException("Remote tab completion timed out.");
            }

            var raw = await tcs.Task.ConfigureAwait(false);
            return ParseCompletionOutput(raw);
        }
        finally
        {
            lock (_completionSync)
            {
                _completionCaptureActive = false;
                _completionTcs = null;
                _completionBuffer.Clear();
            }
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        DisconnectInternalAsync(cancellationToken);

    public void Dispose()
    {
        _readCts?.Cancel();
        try
        {
            _readTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Best-effort shutdown.
        }

        lock (_sync)
        {
            _shell?.Dispose();
            _shell = null;
            _client?.Dispose();
            _client = null;
            try
            {
                _jumpForward?.Stop();
            }
            catch
            {
                // Best-effort.
            }

            _jumpForward = null;
            _jumpClient?.Dispose();
            _jumpClient = null;
        }

        _readCts?.Dispose();
        _readCts = null;
    }

    private async Task CleanupJumpAsync()
    {
        await Task.Run(() =>
        {
            lock (_sync)
            {
                try
                {
                    _jumpForward?.Stop();
                }
                catch
                {
                    // Best-effort.
                }

                _jumpForward = null;
                try
                {
                    if (_jumpClient?.IsConnected == true)
                    {
                        _jumpClient.Disconnect();
                    }
                }
                catch
                {
                    // Best-effort.
                }

                _jumpClient?.Dispose();
                _jumpClient = null;
                _shell?.Dispose();
                _shell = null;
                _client?.Dispose();
                _client = null;
            }
        }).ConfigureAwait(false);
    }

    private async Task ConnectInternalAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken)
    {
        var client = new SshClient(connectionInfo)
        {
            ConnectionInfo = { Timeout = TimeSpan.FromSeconds(20) }
        };

        await Task.Run(() => client.Connect(), cancellationToken).ConfigureAwait(false);

        var shell = client.CreateShellStream("xterm", 120, 40, 800, 600, 8192);
        lock (_sync)
        {
            _client = client;
            _shell = shell;
        }

        _readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readTask = Task.Run(() => ReadShellLoopAsync(shell, _readCts.Token), CancellationToken.None);

        State = SshConnectionState.Connected;
        RaiseOutput($"Connected to {connectionInfo.Host}:{connectionInfo.Port} as {connectionInfo.Username}.");
    }

    private async Task ReadShellLoopAsync(ShellStream shell, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (shell.DataAvailable)
                {
                    var read = await shell.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (read > 0)
                    {
                        var text = Encoding.UTF8.GetString(buffer, 0, read);
                        if (HandleCompletionCapture(text))
                        {
                            continue;
                        }

                        RaiseOutput(text.TrimEnd('\r', '\n'));
                    }
                }
                else
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on disconnect.
        }
        catch (Exception ex)
        {
            RaiseOutput($"Session read error: {ex.Message}", isError: true);
        }
    }

    private async Task DisconnectInternalAsync(CancellationToken cancellationToken = default)
    {
        if (State == SshConnectionState.Disconnected && _client is null)
        {
            return;
        }

        State = SshConnectionState.Disconnecting;

        _readCts?.Cancel();
        if (_readTask is not null)
        {
            try
            {
                await _readTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
            catch (Exception)
            {
                // Best-effort shutdown.
            }
        }

        lock (_sync)
        {
            try
            {
                if (_client?.IsConnected == true)
                {
                    _client.Disconnect();
                }
            }
            catch (Exception)
            {
                // Best-effort shutdown.
            }

            _shell?.Dispose();
            _shell = null;
            _client?.Dispose();
            _client = null;

            try
            {
                _jumpForward?.Stop();
            }
            catch (Exception)
            {
                // Best-effort shutdown.
            }

            _jumpForward = null;

            try
            {
                if (_jumpClient?.IsConnected == true)
                {
                    _jumpClient.Disconnect();
                }
            }
            catch (Exception)
            {
                // Best-effort shutdown.
            }

            _jumpClient?.Dispose();
            _jumpClient = null;
        }

        _readCts?.Dispose();
        _readCts = null;
        _readTask = null;
        State = SshConnectionState.Disconnected;
        RaiseOutput("Disconnected.");
    }

    private bool HandleCompletionCapture(string text)
    {
        if (!_completionCaptureActive)
        {
            return false;
        }

        lock (_completionSync)
        {
            if (!_completionCaptureActive)
            {
                return false;
            }

            _completionBuffer.Append(text);
            var content = _completionBuffer.ToString();
            if (content.Contains(CompletionEndMarker, StringComparison.Ordinal))
            {
                _completionTcs?.TrySetResult(content);
            }
        }

        return true;
    }

    private static string BuildCompletionScript(string lineBeforeCursor, int cursorPosition)
    {
        var escapedLine = EscapeForSingleQuotedBash(lineBeforeCursor);
        var point = Math.Clamp(cursorPosition, 0, lineBeforeCursor.Length);

        var script =
            " bash -c 'echo " + CompletionStartMarker +
            "; line='\"'\"'" + escapedLine + "'\"'\"'; point=" + point.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "; before=\"${line:0:point}\"; word=\"${before##* }\"; trimmed=\"${before#\"${before%%[![:space:]]*}\"}\"" +
            "; if [[ \"$trimmed\" == *\" \" ]]; then compgen -f -- \"$word\" 2>/dev/null;" +
            " else { compgen -c -- \"$word\"; compgen -a -- \"$word\"; compgen -b -- \"$word\"; } 2>/dev/null | sort -u; fi;" +
            " echo " + CompletionEndMarker + "'\n";

        return script;
    }

    private static string EscapeForSingleQuotedBash(string value) =>
        value.Replace("'", "'\"'\"'", StringComparison.Ordinal);

    private static IReadOnlyList<string> ParseCompletionOutput(string raw)
    {
        var startIdx = raw.IndexOf(CompletionStartMarker, StringComparison.Ordinal);
        var endIdx = raw.IndexOf(CompletionEndMarker, StringComparison.Ordinal);
        if (startIdx < 0 || endIdx <= startIdx)
        {
            return Array.Empty<string>();
        }

        var body = raw[(startIdx + CompletionStartMarker.Length)..endIdx];
        return body
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line =>
                line.Length > 0
                && !string.Equals(line, CompletionStartMarker, StringComparison.Ordinal)
                && !string.Equals(line, CompletionEndMarker, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private void RaiseOutput(string text, bool isError = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        OutputReceived?.Invoke(this, new SshConnectionEventArgs(text, isError));
    }
}
