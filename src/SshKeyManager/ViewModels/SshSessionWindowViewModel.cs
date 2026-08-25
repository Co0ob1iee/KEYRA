using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Services;
using SshKeyManager.Services.Ssh;

namespace SshKeyManager.ViewModels;

public partial class SshSessionWindowViewModel : LocalizedViewModelBase, ITerminalSessionHost, IDisposable
{
    private static readonly string[] LocalizedPropertyNames =
    [
        nameof(SessionLabel), nameof(ClearLabel), nameof(SendLabel), nameof(DisconnectLabel)
    ];

    private readonly ISshSessionScope _scope;
    private readonly IAppLogService _log;
    private readonly SshSessionLaunchRequest _request;
    private bool _disposed;
    private bool _closeRequested;

    public SshSessionWindowViewModel(
        ISshSessionScope scope,
        SshSessionLaunchRequest request,
        IAppLogService log,
        ILocalizationService localization)
        : base(localization)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        SessionId = scope.SessionId;
        WindowTitle = request.BuildDisplayTitle();
        HostSummary = $"{request.Username}@{request.Host}:{request.Port}";

        _scope.Coordinator.PropertyChanged += OnCoordinatorPropertyChanged;
        _scope.Terminal.PropertyChanged += OnTerminalPropertyChanged;
        _scope.Coordinator.ConfigureShell(_ => { });
        _scope.Terminal.ConfigureShell(_ => { });
    }

    public Guid SessionId { get; }

    public ITerminalInputController Terminal => _scope.Terminal;

    public ObservableCollection<string> FilteredSuggestions => _scope.Terminal.FilteredSuggestions;

    public string SessionOutput => _scope.Coordinator.SessionOutput;

    public string ConnectionStatus => _scope.Coordinator.ConnectionStatus;

    public bool IsConnected => _scope.Coordinator.IsConnected;

    public bool IsBusy => _scope.Coordinator.IsBusy;

    public string GhostSuggestion => _scope.Terminal.GhostSuggestion;

    public bool IsCompleting => _scope.Terminal.IsCompleting;

    public bool IsTerminalInputEnabled => _scope.Terminal.IsTerminalInputEnabled;

    public string WindowTitle { get; private set; }

    public string HostSummary { get; }

    public string SessionLabel => L("Connections_Session");

    public string ClearLabel => L("Connections_Clear");

    public string SendLabel => L("Connections_Send");

    public string DisconnectLabel => L("Connections_Disconnect");

    public string CommandText
    {
        get => _scope.Terminal.CommandText;
        set => _scope.Terminal.CommandText = value;
    }

    public bool IsSuggestionPopupOpen
    {
        get => _scope.Terminal.IsSuggestionPopupOpen;
        set => _scope.Terminal.IsSuggestionPopupOpen = value;
    }

    public int SelectedSuggestionIndex
    {
        get => _scope.Terminal.SelectedSuggestionIndex;
        set => _scope.Terminal.SelectedSuggestionIndex = value;
    }

    public event EventHandler? RequestClose;

    public event EventHandler? SessionStateChanged;

    IAsyncRelayCommand ITerminalSessionHost.SendCommandCommand => SendCommandCommand;

    protected override void OnLocalizationChanged(string key)
    {
        foreach (var name in LocalizedPropertyNames)
        {
            OnPropertyChanged(name);
        }

        _scope.Coordinator.RefreshConnectionStatusLabel();
    }

    public async Task ConnectAsync()
    {
        if (_request.UsePasswordAuth && string.IsNullOrEmpty(_request.Password))
        {
            _scope.Coordinator.AppendOutput(L("Connections_ErrPasswordRequired"), isError: true);
            return;
        }

        await _scope.Coordinator.ConnectAsync(
            _request.Host,
            _request.Port,
            _request.Username,
            _request.UsePasswordAuth,
            _request.Password,
            _request.SelectedKey,
            _request.KeyPassphrase,
            _request.ProfileId,
            _request.JumpHost,
            _request.JumpHostKey,
            _request.JumpHostKeyPassphrase).ConfigureAwait(true);

        SessionStateChanged?.Invoke(this, EventArgs.Empty);
        NotifySessionBindings();
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        await DisconnectInternalAsync().ConfigureAwait(true);
        if (!_closeRequested)
        {
            _closeRequested = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSendCommand))]
    private async Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(CommandText))
        {
            return;
        }

        var command = CommandText;
        try
        {
            _scope.Terminal.AddCommandToHistory(command);
            await _scope.Coordinator.SendRawCommandAsync(command).ConfigureAwait(true);
            _scope.Terminal.ResetInputAfterSend();
        }
        catch (Exception ex)
        {
            _log.Error($"SSH command failed: {ex.Message}");
            _scope.Coordinator.AppendOutput(ex.Message, isError: true);
        }
    }

    [RelayCommand]
    private void ClearOutput() => _scope.Coordinator.ClearOutput();

    public async Task PrepareCloseAsync()
    {
        _closeRequested = true;
        await DisconnectInternalAsync().ConfigureAwait(true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scope.Coordinator.PropertyChanged -= OnCoordinatorPropertyChanged;
        _scope.Terminal.PropertyChanged -= OnTerminalPropertyChanged;
        _scope.Dispose();
    }

    private async Task DisconnectInternalAsync()
    {
        try
        {
            if (_scope.Coordinator.IsConnected || _scope.Coordinator.IsBusy)
            {
                await _scope.Coordinator.DisconnectAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _log.Error($"SSH session disconnect failed: {ex.Message}");
        }
        finally
        {
            SessionStateChanged?.Invoke(this, EventArgs.Empty);
            NotifySessionBindings();
        }
    }

    private bool CanDisconnect() => !IsBusy && IsConnected;

    private bool CanSendCommand() => IsConnected && !IsBusy && !IsCompleting;

    private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifySessionBindings();
        DisconnectCommand.NotifyCanExecuteChanged();
        SendCommandCommand.NotifyCanExecuteChanged();
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTerminalPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(string.IsNullOrEmpty(e.PropertyName) ? string.Empty : e.PropertyName);
        if (e.PropertyName is nameof(ITerminalInputController.IsCompleting)
            or nameof(ITerminalInputController.IsTerminalInputEnabled)
            or null
            or "")
        {
            SendCommandCommand.NotifyCanExecuteChanged();
        }
    }

    private void NotifySessionBindings()
    {
        OnPropertyChanged(nameof(SessionOutput));
        OnPropertyChanged(nameof(ConnectionStatus));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(GhostSuggestion));
        OnPropertyChanged(nameof(IsCompleting));
        OnPropertyChanged(nameof(IsTerminalInputEnabled));
        OnPropertyChanged(nameof(CommandText));
    }
}
