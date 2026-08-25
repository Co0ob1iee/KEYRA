using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Models;
using SshKeyManager.Presentation;
using SshKeyManager.Services;

namespace SshKeyManager.ViewModels;

public partial class ImportKeyViewModel : LocalizedViewModelBase
{
    private readonly IOpenSshKeyFactory _factory;
    private readonly IVaultStore _vault;
    private readonly IAppLogService _log;
    private readonly IDialogService _dialogs;
    private Action<string> _setStatus = _ => { };
    private Func<Task> _onKeyImportedAsync = () => Task.CompletedTask;

    public ImportKeyViewModel(
        IOpenSshKeyFactory factory,
        IVaultStore vault,
        IAppLogService log,
        ILocalizationService localization,
        IDialogService dialogs)
        : base(localization)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    public void ConfigureShell(Action<string> setStatus, Func<Task> onKeyImportedAsync)
    {
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _onKeyImportedAsync = onKeyImportedAsync ?? throw new ArgumentNullException(nameof(onKeyImportedAsync));
    }

    public string Title => L("Import_Title");

    public string NameLabel => L("Import_Name");

    public string PrivateKeyLabel => L("Import_PrivateKey");

    public string BrowseLabel => L("Import_Browse");

    public string PassphraseLabel => L("Import_Passphrase");

    public string PreviewLabel => L("Import_Preview");

    public string ImportButtonLabel => L("Import_Button");

    public string FingerprintLabel => L("Import_Fingerprint");

    public string PublicKeyLabel => L("Import_PublicKey");

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _privateKeyPem = string.Empty;

    [ObservableProperty]
    private string _passphrase = string.Empty;

    [ObservableProperty]
    private string? _previewPublicKey;

    [ObservableProperty]
    private string? _previewFingerprint;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private bool _isBusy;

    protected override void OnLocalizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnLocalizationPropertyChanged(sender, e);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(NameLabel));
        OnPropertyChanged(nameof(PrivateKeyLabel));
        OnPropertyChanged(nameof(BrowseLabel));
        OnPropertyChanged(nameof(PassphraseLabel));
        OnPropertyChanged(nameof(PreviewLabel));
        OnPropertyChanged(nameof(ImportButtonLabel));
        OnPropertyChanged(nameof(FingerprintLabel));
        OnPropertyChanged(nameof(PublicKeyLabel));
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var path = _dialogs.PickOpenFile(
            L("Import_DialogTitle"),
            "SSH private key|*.*;id_*;*.pem|All files|*.*");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            PrivateKeyPem = await File.ReadAllTextAsync(path).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(Name))
            {
                Name = Path.GetFileName(path);
            }

            StatusText = L("Import_LoadedFile", path);
            await PreviewAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            _log.Error(ex.Message);
        }
    }

    [RelayCommand]
    private Task PreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(PrivateKeyPem))
        {
            StatusText = L("Import_PasteFirst");
            return Task.CompletedTask;
        }

        try
        {
            var imported = _factory.ParsePrivateKey(
                PrivateKeyPem,
                string.IsNullOrEmpty(Passphrase) ? null : Passphrase);
            PreviewPublicKey = imported.PublicKey;
            PreviewFingerprint = imported.Fingerprint;
            StatusText = L("Import_Parsed", imported.Algorithm, imported.Fingerprint);
        }
        catch (Exception ex)
        {
            PreviewPublicKey = null;
            PreviewFingerprint = null;
            StatusText = ex.Message;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusText = L("Import_NameRequired");
            return;
        }

        if (string.IsNullOrWhiteSpace(PrivateKeyPem))
        {
            StatusText = L("Import_KeyRequired");
            return;
        }

        IsBusy = true;
        _setStatus(L("Status_Importing"));
        try
        {
            var imported = _factory.ParsePrivateKey(
                PrivateKeyPem,
                string.IsNullOrEmpty(Passphrase) ? null : Passphrase);

            var record = new SshKeyRecord
            {
                Id = Guid.NewGuid(),
                Name = Name.Trim(),
                Algorithm = imported.Algorithm,
                Comment = imported.Comment,
                PublicKey = imported.PublicKey,
                Fingerprint = imported.Fingerprint,
                CreatedUtc = DateTime.UtcNow,
                HasPassphrase = imported.HasPassphrase
            };

            await _vault.SaveAsync(record, imported.PrivateKeyPem).ConfigureAwait(true);
            PreviewPublicKey = imported.PublicKey;
            PreviewFingerprint = imported.Fingerprint;
            StatusText = L("Import_Success");
            Passphrase = string.Empty;
            PrivateKeyPem = string.Empty;
            _log.Info(L("Log_Imported", record.Name, record.Fingerprint));
            _setStatus(L("Status_KeyImported"));
            await _onKeyImportedAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            _log.Error(ex.Message);
            _setStatus(L("Status_ImportFailed"));
        }
        finally
        {
            IsBusy = false;
        }
    }
}
