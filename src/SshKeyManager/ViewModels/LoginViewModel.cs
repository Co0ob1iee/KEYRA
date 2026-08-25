using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Services;
using SshKeyManager.Services.Security;

namespace SshKeyManager.ViewModels;

public partial class LoginViewModel : LocalizedViewModelBase
{
    private readonly IVaultSecurityService _security;

    public LoginViewModel(IVaultSecurityService security, ILocalizationService localization)
        : base(localization)
    {
        _security = security ?? throw new ArgumentNullException(nameof(security));
    }

    public bool DialogAccepted { get; private set; }

    public string WindowTitle => L("Login_WindowTitle");

    public string Title => L("Login_Title");

    public string Subtitle => L("Login_Subtitle");

    public string UsernameLabel => L("Login_Username");

    public string PasswordLabel => L("Login_Password");

    public string ExitLabel => L("Login_Exit");

    public string UnlockLabel => L("Login_Unlock");

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    protected override void OnLocalizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnLocalizationPropertyChanged(sender, e);
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(UsernameLabel));
        OnPropertyChanged(nameof(PasswordLabel));
        OnPropertyChanged(nameof(ExitLabel));
        OnPropertyChanged(nameof(UnlockLabel));
    }

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            await _security.UnlockAsync(Username.Trim(), Password).ConfigureAwait(true);
            DialogAccepted = true;
            RequestClose?.Invoke(this, true);
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = L("Login_InvalidCredentials");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanUnlock() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => UnlockCommand.NotifyCanExecuteChanged();

    public event EventHandler<bool>? RequestClose;
}
