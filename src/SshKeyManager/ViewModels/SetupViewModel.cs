using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Services;
using SshKeyManager.Services.Security;

namespace SshKeyManager.ViewModels;

public partial class SetupViewModel : LocalizedViewModelBase
{
    private readonly IVaultSecurityService _security;

    public SetupViewModel(IVaultSecurityService security, ILocalizationService localization)
        : base(localization)
    {
        _security = security ?? throw new ArgumentNullException(nameof(security));
    }

    public bool DialogAccepted { get; private set; }

    public string WindowTitle => L("Setup_WindowTitle");

    public string Title => L("Setup_Title");

    public string Subtitle => L("Setup_Subtitle");

    public string UsernameLabel => L("Setup_Username");

    public string PasswordLabel => L("Setup_Password");

    public string ConfirmPasswordLabel => L("Setup_ConfirmPassword");

    public string CancelLabel => L("Setup_Cancel");

    public string CreateLabel => L("Setup_Create");

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

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
        OnPropertyChanged(nameof(ConfirmPasswordLabel));
        OnPropertyChanged(nameof(CancelLabel));
        OnPropertyChanged(nameof(CreateLabel));
    }

    [RelayCommand(CanExecute = nameof(CanComplete))]
    private async Task CompleteAsync()
    {
        ErrorMessage = string.Empty;

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = L("Setup_PasswordMismatch");
            return;
        }

        IsBusy = true;
        try
        {
            await _security.CompleteSetupAsync(Username.Trim(), Password).ConfigureAwait(true);
            DialogAccepted = true;
            RequestClose?.Invoke(this, true);
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
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

    private bool CanComplete() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => CompleteCommand.NotifyCanExecuteChanged();

    public event EventHandler<bool>? RequestClose;
}
