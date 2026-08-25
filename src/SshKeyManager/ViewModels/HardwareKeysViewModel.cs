using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Presentation;
using SshKeyManager.Services;
using SshKeyManager.Services.Agent;
using SshKeyManager.Services.Hardware;

namespace SshKeyManager.ViewModels;

public partial class HardwareKeysViewModel : LocalizedViewModelBase
{
    private readonly IHardwareKeyService _hardware;
    private readonly ISshAgentClient _agentClient;
    private readonly IKeyraAgentProvider _agentProvider;
    private readonly IAppLogService _log;
    private readonly IDialogService _dialogs;
    private Action<string> _setStatus = _ => { };

    public HardwareKeysViewModel(
        IHardwareKeyService hardware,
        ISshAgentClient agentClient,
        IKeyraAgentProvider agentProvider,
        IAppLogService log,
        IDialogService dialogs,
        ILocalizationService localization)
        : base(localization)
    {
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        _agentClient = agentClient ?? throw new ArgumentNullException(nameof(agentClient));
        _agentProvider = agentProvider ?? throw new ArgumentNullException(nameof(agentProvider));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    public ObservableCollection<HardwareSecurityKeyInfo> Keys { get; } = new();

    public ObservableCollection<SshAgentIdentity> AgentIdentities { get; } = new();

    [ObservableProperty] private string _pairName = "YubiKey";

    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private bool _agentRunning;

    [ObservableProperty] private HardwareSecurityKeyInfo? _selectedKey;

    public string Title => L("Hardware_Title");

    public string PairButtonLabel => L("Hardware_Pair");

    public string RefreshLabel => L("Hardware_Refresh");

    public string TestTouchLabel => L("Hardware_TestTouch");

    public string DeleteLabel => L("Hardware_Delete");

    public string AvailabilityText => _hardware.AvailabilityMessage;

    public string AgentSectionTitle => L("Hardware_AgentTitle");

    public string AgentHint => L("Hardware_AgentHint");

    public string StartAgentLabel => L("Hardware_StartAgent");

    public string StopAgentLabel => L("Hardware_StopAgent");

    public string ListSystemAgentLabel => L("Hardware_ListSystemAgent");

    public bool CanPair => _hardware.IsFido2PairingAvailable && !IsBusy;

    public void ConfigureShell(Action<string> setStatus)
    {
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync().ConfigureAwait(true);
        AgentRunning = _agentProvider.IsRunning;
    }

    protected override void OnLocalizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnLocalizationPropertyChanged(sender, e);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PairButtonLabel));
        OnPropertyChanged(nameof(RefreshLabel));
        OnPropertyChanged(nameof(TestTouchLabel));
        OnPropertyChanged(nameof(DeleteLabel));
        OnPropertyChanged(nameof(AvailabilityText));
        OnPropertyChanged(nameof(AgentSectionTitle));
        OnPropertyChanged(nameof(AgentHint));
        OnPropertyChanged(nameof(StartAgentLabel));
        OnPropertyChanged(nameof(StopAgentLabel));
        OnPropertyChanged(nameof(ListSystemAgentLabel));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            Keys.Clear();
            foreach (var key in await _hardware.ListAsync().ConfigureAwait(true))
            {
                Keys.Add(key);
            }

            StatusMessage = L("Hardware_Loaded", Keys.Count);
            OnPropertyChanged(nameof(CanPair));
            OnPropertyChanged(nameof(AvailabilityText));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _log.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecutePair))]
    private async Task PairAsync()
    {
        if (string.IsNullOrWhiteSpace(PairName))
        {
            _dialogs.ShowError(L("Hardware_ErrName"), L("Dialog_Title"));
            return;
        }

        IsBusy = true;
        PairCommand.NotifyCanExecuteChanged();
        try
        {
            StatusMessage = L("Hardware_TouchPrompt");
            _setStatus(StatusMessage);
            var key = await _hardware.PairSkEd25519Async(PairName.Trim()).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SelectedKey = Keys.FirstOrDefault(k => k.Id == key.Id);
            StatusMessage = L("Hardware_Paired", key.DisplayName);
            _setStatus(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _log.Error(ex.Message);
            _dialogs.ShowError(ex.Message, L("Dialog_Title"));
        }
        finally
        {
            IsBusy = false;
            PairCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanExecutePair() => _hardware.IsFido2PairingAvailable && !IsBusy;

    [RelayCommand(CanExecute = nameof(HasSelectedKey))]
    private async Task TestTouchAsync()
    {
        if (SelectedKey is null)
        {
            return;
        }

        try
        {
            var ok = await _hardware.TestTouchAsync(SelectedKey.Id).ConfigureAwait(true);
            StatusMessage = ok ? L("Hardware_TestOk") : L("Hardware_TestFail");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _log.Error(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedKey))]
    private async Task DeleteAsync()
    {
        if (SelectedKey is null)
        {
            return;
        }

        if (!_dialogs.Confirm(L("Hardware_ConfirmDelete", SelectedKey.DisplayName), L("Dialog_Title"), isWarning: true))
        {
            return;
        }

        try
        {
            await _hardware.DeleteAsync(SelectedKey.Id).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _log.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task StartAgentAsync()
    {
        try
        {
            await _agentProvider.StartAsync().ConfigureAwait(true);
            AgentRunning = _agentProvider.IsRunning;
            StatusMessage = L("Hardware_AgentStarted", _agentProvider.PipeName);
            _setStatus(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _dialogs.ShowError(ex.Message, L("Dialog_Title"));
        }
    }

    [RelayCommand]
    private async Task StopAgentAsync()
    {
        await _agentProvider.StopAsync().ConfigureAwait(true);
        AgentRunning = false;
        StatusMessage = L("Hardware_AgentStopped");
    }

    [RelayCommand]
    private async Task ListSystemAgentAsync()
    {
        AgentIdentities.Clear();
        try
        {
            if (!_agentClient.IsAvailable)
            {
                StatusMessage = L("Hardware_SystemAgentMissing");
                return;
            }

            foreach (var id in await _agentClient.ListIdentitiesAsync().ConfigureAwait(true))
            {
                AgentIdentities.Add(id);
            }

            StatusMessage = L("Hardware_SystemAgentCount", AgentIdentities.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _log.Error(ex.Message);
        }
    }

    private bool HasSelectedKey() => SelectedKey is not null;

    partial void OnSelectedKeyChanged(HardwareSecurityKeyInfo? value)
    {
        TestTouchCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value) => PairCommand.NotifyCanExecuteChanged();
}
