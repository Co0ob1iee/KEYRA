using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Models;
using SshKeyManager.Presentation;
using SshKeyManager.Services;
using SshKeyManager.Services.Security;
using SshKeyManager.Services.Update;

namespace SshKeyManager.ViewModels;

public partial class SettingsViewModel : LocalizedViewModelBase
{
    private readonly IVaultStore _vault;
    private readonly IKeyExportService _export;
    private readonly IAppLogService _log;
    private readonly IVaultSecurityService _security;
    private readonly IAppSettingsService _settingsService;
    private readonly IAppUpdateService _updates;
    private readonly IDialogService _dialogs;
    private Action<string> _setStatus = _ => { };
    private Action _onCultureChanged = () => { };
    private bool _suppressLanguageChange;
    private bool _suppressUpdateSettingsSave;

    public SettingsViewModel(
        IVaultStore vault,
        IKeyExportService export,
        IAppLogService log,
        IVaultSecurityService security,
        ILocalizationService localization,
        IAppSettingsService settingsService,
        IAppUpdateService updates,
        IDialogService dialogs,
        HardwareKeysViewModel hardware)
        : base(localization)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _security = security ?? throw new ArgumentNullException(nameof(security));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _updates = updates ?? throw new ArgumentNullException(nameof(updates));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        Hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));

        LanguageOptions = LanguageOption.All;
        _suppressLanguageChange = true;
        SelectedLanguage = LanguageOptions.FirstOrDefault(o =>
            o.CultureName.Equals(_settingsService.Settings.Language, StringComparison.OrdinalIgnoreCase))
            ?? LanguageOptions.First();
        _suppressLanguageChange = false;

        _suppressUpdateSettingsSave = true;
        UpdateGitHubOwner = _settingsService.Settings.UpdateGitHubOwner;
        UpdateGitHubRepo = string.IsNullOrWhiteSpace(_settingsService.Settings.UpdateGitHubRepo)
            ? "KEYRA"
            : _settingsService.Settings.UpdateGitHubRepo;
        CheckForUpdatesOnStartup = _settingsService.Settings.CheckForUpdatesOnStartup;
        _suppressUpdateSettingsSave = false;

        CurrentAppVersion = $"KEYRA v{_updates.GetCurrentVersion().ToString(3)}";
        RefreshPaths();
    }

    public HardwareKeysViewModel Hardware { get; }

    public void ConfigureShell(Action<string> setStatus, Action onCultureChanged)
    {
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _onCultureChanged = onCultureChanged ?? throw new ArgumentNullException(nameof(onCultureChanged));
        Hardware.ConfigureShell(setStatus);
        _ = Hardware.InitializeAsync();
    }

    public string DatabasePathLabel => L("Settings_DatabasePath");

    [ObservableProperty]
    private string _databasePath = string.Empty;

    public void RefreshPaths()
    {
        VaultPath = _vault.VaultDirectory;
        RootPath = _vault.RootDirectory;
        DatabasePath = _security.DatabasePath;
        SshPath = _export.GetDefaultSshDirectory();
    }

    public IReadOnlyList<LanguageOption> LanguageOptions { get; }

    public string Title => L("Settings_Title");

    public string InfoText => L("Settings_Info");

    public string RootPathLabel => L("Settings_RootPath");

    public string OpenRootLabel => L("Settings_OpenRoot");

    public string VaultPathLabel => L("Settings_VaultPath");

    public string OpenVaultLabel => L("Settings_OpenVault");

    public string SshPathLabel => L("Settings_SshPath");

    public string LanguageLabel => L("Settings_Language");

    public string ChangePasswordTitle => L("Settings_ChangePassword");

    public string ChangePasswordHint => L("Settings_ChangePasswordHint");

    public string CurrentPasswordLabel => L("Settings_CurrentPassword");

    public string NewPasswordLabel => L("Settings_NewPassword");

    public string ConfirmPasswordLabel => L("Settings_ConfirmPassword");

    public string ChangeButtonLabel => L("Settings_ChangeButton");

    public string SecurityTitle => L("Settings_Security");

    public string SecurityHint => L("Settings_SecurityHint");

    public string UpdatesTitle => L("Settings_UpdatesTitle");

    public string UpdatesHint => L("Settings_UpdatesHint");

    public string UpdatesOwnerLabel => L("Settings_UpdatesOwner");

    public string UpdatesRepoLabel => L("Settings_UpdatesRepo");

    public string UpdatesCheckOnStartupLabel => L("Settings_UpdatesCheckOnStartup");

    public string UpdatesCheckNowLabel => L("Settings_UpdatesCheckNow");

    public string UpdatesOpenReleaseLabel => L("Settings_UpdatesOpenRelease");

    public string UpdatesCurrentVersionLabel => L("Settings_UpdatesCurrentVersion");

    [ObservableProperty]
    private string _vaultPath = string.Empty;

    [ObservableProperty]
    private string _rootPath = string.Empty;

    [ObservableProperty]
    private string _sshPath = string.Empty;

    [ObservableProperty]
    private LanguageOption _selectedLanguage = null!;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmNewPassword = string.Empty;

    [ObservableProperty]
    private string _passwordChangeMessage = string.Empty;

    [ObservableProperty]
    private bool _isChangingPassword;

    [ObservableProperty]
    private string _updateGitHubOwner = string.Empty;

    [ObservableProperty]
    private string _updateGitHubRepo = "KEYRA";

    [ObservableProperty]
    private bool _checkForUpdatesOnStartup = true;

    [ObservableProperty]
    private string _currentAppVersion = string.Empty;

    [ObservableProperty]
    private string _updateStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    private bool _isApplyingUpdate;

    [ObservableProperty]
    private double _updateDownloadProgress;

    [ObservableProperty]
    private AppUpdateCheckResult? _lastUpdateCheck;

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (_suppressLanguageChange || value is null)
        {
            return;
        }

        _ = ApplyLanguageChangeAsync(value.CultureName);
    }

    partial void OnUpdateGitHubOwnerChanged(string value) => _ = PersistUpdateSettingsAsync();

    partial void OnUpdateGitHubRepoChanged(string value) => _ = PersistUpdateSettingsAsync();

    partial void OnCheckForUpdatesOnStartupChanged(bool value) => _ = PersistUpdateSettingsAsync();

    private async Task PersistUpdateSettingsAsync()
    {
        if (_suppressUpdateSettingsSave)
        {
            return;
        }

        try
        {
            _settingsService.Settings.UpdateGitHubOwner = (UpdateGitHubOwner ?? string.Empty).Trim();
            _settingsService.Settings.UpdateGitHubRepo = string.IsNullOrWhiteSpace(UpdateGitHubRepo)
                ? "KEYRA"
                : UpdateGitHubRepo.Trim();
            _settingsService.Settings.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;
            await _settingsService.SaveAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
        }
    }

    private async Task ApplyLanguageChangeAsync(string cultureName)
    {
        try
        {
            Localization.SetCulture(cultureName);
            await _settingsService.SaveLanguageAsync(cultureName).ConfigureAwait(true);
            _onCultureChanged();
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
        }
    }

    protected override void OnLocalizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnLocalizationPropertyChanged(sender, e);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(InfoText));
        OnPropertyChanged(nameof(RootPathLabel));
        OnPropertyChanged(nameof(OpenRootLabel));
        OnPropertyChanged(nameof(VaultPathLabel));
        OnPropertyChanged(nameof(OpenVaultLabel));
        OnPropertyChanged(nameof(SshPathLabel));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(ChangePasswordTitle));
        OnPropertyChanged(nameof(ChangePasswordHint));
        OnPropertyChanged(nameof(CurrentPasswordLabel));
        OnPropertyChanged(nameof(NewPasswordLabel));
        OnPropertyChanged(nameof(ConfirmPasswordLabel));
        OnPropertyChanged(nameof(ChangeButtonLabel));
        OnPropertyChanged(nameof(SecurityTitle));
        OnPropertyChanged(nameof(SecurityHint));
        OnPropertyChanged(nameof(DatabasePathLabel));
        OnPropertyChanged(nameof(UpdatesTitle));
        OnPropertyChanged(nameof(UpdatesHint));
        OnPropertyChanged(nameof(UpdatesOwnerLabel));
        OnPropertyChanged(nameof(UpdatesRepoLabel));
        OnPropertyChanged(nameof(UpdatesCheckOnStartupLabel));
        OnPropertyChanged(nameof(UpdatesCheckNowLabel));
        OnPropertyChanged(nameof(UpdatesOpenReleaseLabel));
        OnPropertyChanged(nameof(UpdatesCurrentVersionLabel));
    }

    [RelayCommand]
    private void OpenVaultFolder()
    {
        try
        {
            Directory.CreateDirectory(VaultPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = VaultPath,
                UseShellExecute = true
            });
            _log.Info(L("Log_OpenVault"));
            _setStatus(L("Status_OpenVaultFolder"));
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
            _setStatus(L("Status_OpenVaultFailed"));
        }
    }

    [RelayCommand]
    private void OpenRootFolder()
    {
        try
        {
            Directory.CreateDirectory(RootPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = RootPath,
                UseShellExecute = true
            });
            _log.Info(L("Log_OpenRoot"));
            _setStatus(L("Status_OpenRootFolder"));
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
            _setStatus(L("Status_OpenRootFailed"));
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangePassword))]
    private async Task ChangePasswordAsync()
    {
        PasswordChangeMessage = string.Empty;

        if (!string.Equals(NewPassword, ConfirmNewPassword, StringComparison.Ordinal))
        {
            PasswordChangeMessage = L("Settings_PasswordMismatch");
            return;
        }

        IsChangingPassword = true;
        try
        {
            await _security.ChangePasswordAsync(CurrentPassword, NewPassword).ConfigureAwait(true);
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmNewPassword = string.Empty;
            PasswordChangeMessage = L("Settings_PasswordChanged");
            _log.Info(L("Log_PasswordChanged"));
            _setStatus(L("Status_PasswordChanged"));
        }
        catch (UnauthorizedAccessException)
        {
            PasswordChangeMessage = L("Settings_WrongPassword");
        }
        catch (Exception ex)
        {
            PasswordChangeMessage = ex.Message;
            _log.Error(ex.Message);
        }
        finally
        {
            IsChangingPassword = false;
        }
    }

    private bool CanChangePassword() => !IsChangingPassword;

    partial void OnIsChangingPasswordChanged(bool value) =>
        ChangePasswordCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        await PersistUpdateSettingsAsync().ConfigureAwait(true);
        IsCheckingUpdates = true;
        UpdateStatusMessage = L("Settings_UpdatesChecking");
        UpdateDownloadProgress = 0;
        try
        {
            var result = await _updates.CheckForUpdatesAsync().ConfigureAwait(true);
            LastUpdateCheck = result;
            await HandleUpdateResultAsync(result, interactive: true).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = L("Settings_UpdatesFailed", ex.Message);
            _log.Error(ex.Message);
            _dialogs.ShowError(UpdateStatusMessage, L("Settings_UpdatesTitle"));
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    private bool CanCheckUpdates() => !IsCheckingUpdates && !IsApplyingUpdate;

    partial void OnIsCheckingUpdatesChanged(bool value)
    {
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        OpenLastReleaseCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsApplyingUpdateChanged(bool value)
    {
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        OpenLastReleaseCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanOpenLastRelease))]
    private void OpenLastRelease()
    {
        if (LastUpdateCheck is null)
        {
            return;
        }

        _updates.OpenReleasePage(LastUpdateCheck);
    }

    private bool CanOpenLastRelease() =>
        LastUpdateCheck is not null && !IsCheckingUpdates && !IsApplyingUpdate;

    public async Task CheckForUpdatesQuietAsync()
    {
        if (!CheckForUpdatesOnStartup)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(UpdateGitHubOwner) || string.IsNullOrWhiteSpace(UpdateGitHubRepo))
        {
            return;
        }

        try
        {
            var result = await _updates.CheckForUpdatesAsync().ConfigureAwait(true);
            LastUpdateCheck = result;
            if (result.Status == AppUpdateStatus.UpdateAvailable)
            {
                await HandleUpdateResultAsync(result, interactive: true).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _log.Error(L("Log_UpdateCheckFailed", ex.Message));
        }
    }

    private async Task HandleUpdateResultAsync(AppUpdateCheckResult result, bool interactive)
    {
        switch (result.Status)
        {
            case AppUpdateStatus.NotConfigured:
                UpdateStatusMessage = L("Settings_UpdatesNotConfigured");
                if (interactive)
                {
                    _dialogs.ShowInfo(UpdateStatusMessage, L("Settings_UpdatesTitle"));
                }

                break;

            case AppUpdateStatus.UpToDate:
                UpdateStatusMessage = L(
                    "Settings_UpdatesUpToDate",
                    result.CurrentVersion?.ToString(3) ?? "?");
                if (interactive)
                {
                    _dialogs.ShowInfo(UpdateStatusMessage, L("Settings_UpdatesTitle"));
                }

                _setStatus(UpdateStatusMessage);
                break;

            case AppUpdateStatus.Failed:
                UpdateStatusMessage = L("Settings_UpdatesFailed", result.Message ?? string.Empty);
                if (interactive)
                {
                    _dialogs.ShowError(UpdateStatusMessage, L("Settings_UpdatesTitle"));
                }

                break;

            case AppUpdateStatus.UpdateAvailable:
                UpdateStatusMessage = L(
                    "Settings_UpdatesAvailable",
                    result.LatestVersion?.ToString(3) ?? "?",
                    result.CurrentVersion?.ToString(3) ?? "?");
                _setStatus(UpdateStatusMessage);
                _log.Info(UpdateStatusMessage);

                var prompt = result.HasDownloadableAsset
                    ? L("Settings_UpdatesConfirmDownload", result.LatestVersion?.ToString(3) ?? "?")
                    : L("Settings_UpdatesConfirmOpenPage", result.LatestVersion?.ToString(3) ?? "?");

                if (!_dialogs.Confirm(prompt, L("Settings_UpdatesTitle")))
                {
                    break;
                }

                if (!result.HasDownloadableAsset)
                {
                    _updates.OpenReleasePage(result);
                    break;
                }

                IsApplyingUpdate = true;
                try
                {
                    var progress = new Progress<double>(p => UpdateDownloadProgress = p);
                    var shouldExit = await _updates.ApplyUpdateAsync(result, progress).ConfigureAwait(true);
                    if (shouldExit)
                    {
                        _log.Info(L("Log_UpdateInstallerStarted"));
                        System.Windows.Application.Current?.Shutdown();
                    }
                    else
                    {
                        UpdateStatusMessage = L("Settings_UpdatesZipReady");
                        _dialogs.ShowInfo(UpdateStatusMessage, L("Settings_UpdatesTitle"));
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatusMessage = L("Settings_UpdatesDownloadFailed", ex.Message);
                    _log.Error(ex.Message);
                    _dialogs.ShowError(UpdateStatusMessage, L("Settings_UpdatesTitle"));
                }
                finally
                {
                    IsApplyingUpdate = false;
                }

                break;
        }
    }
}
