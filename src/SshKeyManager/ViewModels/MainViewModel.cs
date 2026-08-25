using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Models;
using SshKeyManager.Presentation;
using SshKeyManager.Services;
using SshKeyManager.Services.Agent;
using SshKeyManager.Services.Security;
using SshKeyManager.Services.Ssh;
using System.ComponentModel;
using System.Windows;

namespace SshKeyManager.ViewModels;

public partial class MainViewModel : LocalizedViewModelBase
{
    private readonly IAppLogService _log;
    private readonly IVaultSecurityService _security;
    private readonly INavigationService _navigation;
    private readonly ISshSessionWindowService _sessions;
    private readonly IKeyraAgentProvider _agent;

    public MainViewModel(
        KeysViewModel keys,
        GenerateKeyViewModel generate,
        ImportKeyViewModel import,
        ConnectionsViewModel connections,
        SettingsViewModel settings,
        IAppLogService log,
        IVaultSecurityService security,
        ILocalizationService localization,
        INavigationService navigation,
        IShellLayoutService layout,
        ISshSessionWindowService sessions,
        IKeyraAgentProvider agent)
        : base(localization)
    {
        Keys = keys ?? throw new ArgumentNullException(nameof(keys));
        Generate = generate ?? throw new ArgumentNullException(nameof(generate));
        Import = import ?? throw new ArgumentNullException(nameof(import));
        Connections = connections ?? throw new ArgumentNullException(nameof(connections));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _security = security ?? throw new ArgumentNullException(nameof(security));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));

        StatusBar = new StatusBarViewModel(localization, sessions);
        LogPanel = new LogPanelViewModel(log, localization, layout, StatusBar.SetStatus);
        Navigation = new NavigationViewModel(localization, navigation);
        Inspector = new InspectorViewModel(localization, keys);

        ShellModuleBootstrap.Initialize(this, _navigation, layout);
    }

    public KeysViewModel Keys { get; }
    public GenerateKeyViewModel Generate { get; }
    public ImportKeyViewModel Import { get; }
    public ConnectionsViewModel Connections { get; }
    public SettingsViewModel Settings { get; }
    public NavigationViewModel Navigation { get; }
    public StatusBarViewModel StatusBar { get; }
    public LogPanelViewModel LogPanel { get; }
    public InspectorViewModel Inspector { get; }
    public string AppTitle => L("App_Title");

    [ObservableProperty] private ObservableObject _currentViewModel = null!;
    [ObservableProperty] private AppSection _selectedSection;
    [ObservableProperty] private GridLength _logRowHeight = new(160);

    public event EventHandler? RequestRelogin;

    protected override void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        base.OnLocalizationPropertyChanged(sender, e);
        OnPropertyChanged(nameof(AppTitle));
        StatusBar.SetSection(SectionStatus(SelectedSection));
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        try
        {
            await Keys.RefreshAsync().ConfigureAwait(true);
            await Connections.LoadKeysAsync().ConfigureAwait(true);
            await Connections.LoadProfilesAsync().ConfigureAwait(true);
            Settings.RefreshPaths();
            StatusBar.SetKeyCount(Keys.Keys.Count);
            StatusBar.SetStatus(L("Status_Ready"));
            StatusBar.SetVaultUnlocked();
            await TryStartAgentAsync().ConfigureAwait(true);
            _log.Info(L("Log_AppStarted"));
        }
        catch (Exception ex)
        {
            StatusBar.SetStatus(L("Status_StartupError"));
            _log.Error(L("Log_StartupFailed", ex.Message));
        }
    }

    [RelayCommand]
    private async Task LockAndExitAsync()
    {
        try
        {
            await _sessions.DisconnectAllAsync().ConfigureAwait(true);
            await TryStopAgentAsync().ConfigureAwait(true);
            _security.Lock();
            _log.Info(L("Log_VaultLocked"));
            RequestRelogin?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
        }
    }

    internal void SetStatus(string message)
    {
        StatusBar.SetStatus(message);
        StatusBar.SetKeyCount(Keys.Keys.Count);
    }

    internal void NotifyCultureChanged()
    {
        Navigation.RefreshNavigationItems();
        OnPropertyChanged(nameof(AppTitle));
        StatusBar.SetSection(SectionStatus(SelectedSection));
    }

    internal string SectionStatus(AppSection section) => section switch
    {
        AppSection.Keys => L("Nav_Keys"),
        AppSection.Generate => L("Nav_Generate"),
        AppSection.Import => L("Nav_Import"),
        AppSection.Connections => L("Nav_Connections"),
        AppSection.Settings => L("Nav_Settings"),
        _ => L("Status_Ready")
    };

    internal ObservableObject ResolveSection(AppSection section) => section switch
    {
        AppSection.Generate => Generate,
        AppSection.Import => Import,
        AppSection.Connections => Connections,
        AppSection.Settings => Settings,
        _ => Keys
    };

    internal void SyncLogRowHeight() =>
        LogRowHeight = LogPanel.IsExpanded
            ? new GridLength(Math.Max(80, LogPanel.PanelHeight))
            : new GridLength(28);

    private async Task TryStartAgentAsync()
    {
        try
        {
            await _agent.StartAsync().ConfigureAwait(true);
            _log.Info($"KEYRA ssh-agent auto-started on \\\\.\\pipe\\{_agent.PipeName}");
        }
        catch (Exception ex)
        {
            _log.Error($"KEYRA ssh-agent auto-start failed: {ex.Message}");
        }
    }

    private async Task TryStopAgentAsync()
    {
        try
        {
            await _agent.StopAsync().ConfigureAwait(true);
        }
        catch (Exception)
        {
            // Best-effort stop on vault lock.
        }
    }
}
