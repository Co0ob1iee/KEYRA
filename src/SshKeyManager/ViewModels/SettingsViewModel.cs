using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Models;
using SshKeyManager.Services;
using SshKeyManager.Services.Security;

namespace SshKeyManager.ViewModels;

public partial class SettingsViewModel : LocalizedViewModelBase
{
    private readonly IVaultStore _vault;
    private readonly IKeyExportService _export;
    private readonly IAppLogService _log;
    private readonly IVaultSecurityService _security;
    private readonly IAppSettingsService _settingsService;
    private Action<string> _setStatus = _ => { };
    private Action _onCultureChanged = () => { };
    private bool _suppressLanguageChange;

    public SettingsViewModel(
        IVaultStore vault,
        IKeyExportService export,
        IAppLogService log,
        IVaultSecurityService security,
        ILocalizationService localization,
        IAppSettingsService settingsService,
        HardwareKeysViewModel hardware)
        : base(localization)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _security = security ?? throw new ArgumentNullException(nameof(security));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        Hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));

        LanguageOptions = LanguageOption.All;
        _suppressLanguageChange = true;
        SelectedLanguage = LanguageOptions.FirstOrDefault(o =>
            o.CultureName.Equals(_settingsService.Settings.Language, StringComparison.OrdinalIgnoreCase))
            ?? LanguageOptions.First();
        _suppressLanguageChange = false;

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

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (_suppressLanguageChange || value is null)
        {
            return;
        }

        _ = ApplyLanguageChangeAsync(value.CultureName);
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
}
