using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using SshKeyManager.Helpers;
using SshKeyManager.Models;
using SshKeyManager.Services.Data;

namespace SshKeyManager.Services.Ssh;

public sealed partial class SshSessionCoordinator : ObservableObject, ISshSessionCoordinator, IDisposable
{
    private readonly ISshConnectionService _ssh;
    private readonly IVaultStore _vault;
    private readonly IAppLogService _log;
    private readonly ILocalizationService _localization;
    private readonly IConnectionAuditService _audit;
    private Action<string> _setStatus = _ => { };
    private CancellationTokenSource? _sessionCts;
    private readonly StringBuilder _outputBuilder = new();
    private Guid? _auditServerId;
    private bool _disposed;

    public SshSessionCoordinator(
        ISshConnectionService ssh,
        IVaultStore vault,
        IAppLogService log,
        ILocalizationService localization,
        IConnectionAuditService audit)
    {
        _ssh = ssh ?? throw new ArgumentNullException(nameof(ssh));
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));

        _ssh.OutputReceived += OnSshOutput;
        _ssh.StateChanged += OnSshStateChanged;
        ConnectionStatus = L("Connections_Disconnected");
    }

    [ObservableProperty]
    private string _sessionOutput = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _connectionStatus = string.Empty;

    public CancellationToken SessionToken => _sessionCts?.Token ?? CancellationToken.None;

    public void ConfigureShell(Action<string> setStatus)
    {
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
    }

    public async Task ConnectAsync(
        string host,
        int port,
        string username,
        bool usePasswordAuth,
        string password,
        SshKeyRecord? selectedKey,
        string keyPassphrase,
        Guid? auditServerId = null,
        SshConnectionProfile? jumpHost = null,
        SshKeyRecord? jumpHostKey = null,
        string jumpHostKeyPassphrase = "")
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
        {
            AppendOutput(L("Connections_ErrHostUser"), isError: true);
            return;
        }

        if (!usePasswordAuth && selectedKey is null)
        {
            AppendOutput(L("Connections_ErrSelectKey"), isError: true);
            return;
        }

        if (jumpHost is not null && jumpHostKey is null && !usePasswordAuth)
        {
            // Jump with password auth on target still needs a bastion key for KEYRA jump.
        }

        if (jumpHost is not null && jumpHostKey is null)
        {
            AppendOutput(L("Connections_ErrJumpKey"), isError: true);
            return;
        }

        IsBusy = true;
        _auditServerId = auditServerId;
        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _sessionCts = new CancellationTokenSource();

        try
        {
            if (jumpHost is not null)
            {
                using var jumpMaterial = await _vault.LoadPrivateKeyAsync(jumpHostKey!.Id, _sessionCts.Token)
                    .ConfigureAwait(true);
                string? targetPem = null;
                try
                {
                    if (!usePasswordAuth)
                    {
                        using var targetMaterial = await _vault
                            .LoadPrivateKeyAsync(selectedKey!.Id, _sessionCts.Token)
                            .ConfigureAwait(true);
                        targetPem = targetMaterial.GetPrivateKeyPem();
                    }

                    await _ssh.ConnectViaJumpHostAsync(
                        jumpHost.Host.Trim(),
                        jumpHost.Port,
                        jumpHost.Username.Trim(),
                        jumpMaterial.GetPrivateKeyPem(),
                        string.IsNullOrWhiteSpace(jumpHostKeyPassphrase) ? null : jumpHostKeyPassphrase,
                        host.Trim(),
                        port,
                        username.Trim(),
                        usePasswordAuth,
                        password,
                        targetPem,
                        string.IsNullOrWhiteSpace(keyPassphrase) ? null : keyPassphrase,
                        _sessionCts.Token).ConfigureAwait(true);
                }
                finally
                {
                    // targetPem cleared by GC; SecureKeyMaterial already disposed.
                }
            }
            else if (usePasswordAuth)
            {
                await _ssh.ConnectWithPasswordAsync(
                    host.Trim(),
                    port,
                    username.Trim(),
                    password,
                    _sessionCts.Token).ConfigureAwait(true);
            }
            else
            {
                using var material = await _vault.LoadPrivateKeyAsync(selectedKey!.Id, _sessionCts.Token)
                    .ConfigureAwait(true);
                await _ssh.ConnectWithKeyAsync(
                    host.Trim(),
                    port,
                    username.Trim(),
                    material.GetPrivateKeyPem(),
                    string.IsNullOrWhiteSpace(keyPassphrase) ? null : keyPassphrase,
                    _sessionCts.Token).ConfigureAwait(true);
            }

            _log.Info($"SSH connected to {host}:{port}.");
            _setStatus(L("Connections_Connected"));
            await WriteAuditAsync(ConnectionLogStatus.Success, null).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            await WriteAuditAsync(ConnectionLogStatus.Timeout, "Connection cancelled or timed out.")
                .ConfigureAwait(true);
            _setStatus(L("Connections_Disconnected"));
            AppendOutput(L("Connections_ErrTimeout"), isError: true);
        }
        catch (TimeoutException ex)
        {
            _log.Error($"SSH connect timeout: {ex.Message}");
            _setStatus(L("Connections_Disconnected"));
            AppendOutput(ex.Message, isError: true);
            await WriteAuditAsync(ConnectionLogStatus.Timeout, ex.Message).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error($"SSH connect failed: {ex.Message}");
            _setStatus(L("Connections_Disconnected"));
            AppendOutput(ex.Message, isError: true);
            await WriteAuditAsync(ConnectionLogStatus.Failed, ex.Message).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task WriteAuditAsync(ConnectionLogStatus status, string? error)
    {
        if (_auditServerId is not Guid id)
        {
            return;
        }

        try
        {
            await _audit.LogAsync(id, status, error).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to write connection audit log: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            _sessionCts?.Cancel();
            await _ssh.DisconnectAsync().ConfigureAwait(true);
            _log.Info("SSH disconnected.");
            _setStatus(L("Connections_Disconnected"));
        }
        catch (Exception ex)
        {
            _log.Error($"SSH disconnect failed: {ex.Message}");
            AppendOutput(ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SendRawCommandAsync(string command)
    {
        await _ssh.SendCommandAsync(command, SessionToken).ConfigureAwait(true);
    }

    public void ClearOutput()
    {
        _outputBuilder.Clear();
        SessionOutput = string.Empty;
    }

    public void AppendOutput(string text, bool isError = false)
    {
        var line = isError ? L("Connections_OutputError", text) : text;
        _outputBuilder.AppendLine(line);
        SessionOutput = _outputBuilder.ToString();
    }

    public void RefreshConnectionStatusLabel(SshConnectionState? state = null)
    {
        var resolved = state ?? (IsConnected ? SshConnectionState.Connected : SshConnectionState.Disconnected);
        ConnectionStatus = resolved switch
        {
            SshConnectionState.Disconnected => L("Connections_Disconnected"),
            SshConnectionState.Connecting => L("Connections_Connecting"),
            SshConnectionState.Connected => L("Connections_Connected"),
            SshConnectionState.Disconnecting => L("Connections_Disconnecting"),
            _ => resolved.ToString()
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ssh.OutputReceived -= OnSshOutput;
        _ssh.StateChanged -= OnSshStateChanged;
        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _sessionCts = null;
    }

    private void OnSshOutput(object? sender, SshConnectionEventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => OnSshOutput(sender, e));
            return;
        }

        // Keep raw ANSI for the terminal control; strip escapes before AppLog.
        AppendOutput(e.Text, e.IsError);
        var plain = AnsiTerminalParser.Strip(e.Text);
        if (string.IsNullOrWhiteSpace(plain))
        {
            return;
        }

        if (e.IsError)
        {
            _log.Error(plain);
        }
        else
        {
            _log.Info(plain);
        }
    }

    private void OnSshStateChanged(object? sender, SshConnectionState state)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => OnSshStateChanged(sender, state));
            return;
        }

        IsConnected = state == SshConnectionState.Connected;
        RefreshConnectionStatusLabel(state);
    }

    private string L(string key) => _localization.GetString(key);

    private string L(string key, params object[] args) => _localization.GetString(key, args);
}
