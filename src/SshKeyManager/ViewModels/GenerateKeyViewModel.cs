using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Models;
using SshKeyManager.Services;

namespace SshKeyManager.ViewModels;

public partial class GenerateKeyViewModel : LocalizedViewModelBase
{
    private readonly IOpenSshKeyFactory _factory;
    private readonly IVaultStore _vault;
    private readonly IAppLogService _log;
    private Action<string> _setStatus = _ => { };
    private Func<Task> _onKeyCreatedAsync = () => Task.CompletedTask;

    public GenerateKeyViewModel(
        IOpenSshKeyFactory factory,
        IVaultStore vault,
        IAppLogService log,
        ILocalizationService localization)
        : base(localization)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        Name = $"id_{Environment.UserName}".ToLowerInvariant();
        Comment = $"{Environment.UserName}@{Environment.MachineName}";
    }

    public void ConfigureShell(Action<string> setStatus, Func<Task> onKeyCreatedAsync)
    {
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _onKeyCreatedAsync = onKeyCreatedAsync ?? throw new ArgumentNullException(nameof(onKeyCreatedAsync));
    }

    public IReadOnlyList<SshKeyAlgorithm> Algorithms { get; } =
    [
        SshKeyAlgorithm.Ed25519,
        SshKeyAlgorithm.Rsa4096,
        SshKeyAlgorithm.EcdsaP384
    ];

    public string Title => L("Generate_Title");

    public string NameLabel => L("Generate_Name");

    public string CommentLabel => L("Generate_Comment");

    public string AlgorithmLabel => L("Generate_Algorithm");

    public string PassphraseLabel => L("Generate_Passphrase");

    public string ConfirmPassphraseLabel => L("Generate_ConfirmPassphrase");

    public string GenerateButtonLabel => L("Generate_Button");

    public string BusyLabel => L("Generate_Busy");

    public string FingerprintLabel => L("Generate_Fingerprint");

    public string PublicKeyLabel => L("Generate_PublicKey");

    public string Ed25519Label => L("Generate_Algorithm_Ed25519");

    public string Rsa4096Label => L("Generate_Algorithm_Rsa4096");

    public string EcdsaP384Label => L("Generate_Algorithm_EcdsaP384");

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _comment = string.Empty;

    [ObservableProperty]
    private SshKeyAlgorithm _selectedAlgorithm = SshKeyAlgorithm.Ed25519;

    [ObservableProperty]
    private string _passphrase = string.Empty;

    [ObservableProperty]
    private string _passphraseConfirm = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _lastPublicKey;

    [ObservableProperty]
    private string? _lastFingerprint;

    [ObservableProperty]
    private string? _resultMessage;

    protected override void OnLocalizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnLocalizationPropertyChanged(sender, e);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(NameLabel));
        OnPropertyChanged(nameof(CommentLabel));
        OnPropertyChanged(nameof(AlgorithmLabel));
        OnPropertyChanged(nameof(PassphraseLabel));
        OnPropertyChanged(nameof(ConfirmPassphraseLabel));
        OnPropertyChanged(nameof(GenerateButtonLabel));
        OnPropertyChanged(nameof(BusyLabel));
        OnPropertyChanged(nameof(FingerprintLabel));
        OnPropertyChanged(nameof(PublicKeyLabel));
        OnPropertyChanged(nameof(Ed25519Label));
        OnPropertyChanged(nameof(Rsa4096Label));
        OnPropertyChanged(nameof(EcdsaP384Label));
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            ResultMessage = L("Generate_NameRequired");
            return;
        }

        if (!string.Equals(Passphrase, PassphraseConfirm, StringComparison.Ordinal))
        {
            ResultMessage = L("Generate_PassphraseMismatch");
            return;
        }

        IsBusy = true;
        ResultMessage = null;
        LastPublicKey = null;
        LastFingerprint = null;
        _setStatus(L("Status_Generating"));

        try
        {
            var request = new GenerateKeyRequest
            {
                Name = Name.Trim(),
                Comment = Comment?.Trim() ?? string.Empty,
                Algorithm = SelectedAlgorithm,
                Passphrase = string.IsNullOrEmpty(Passphrase) ? null : Passphrase
            };

            var generated = await _factory.GenerateAsync(request).ConfigureAwait(true);
            var record = new SshKeyRecord
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Algorithm = generated.Algorithm,
                Comment = generated.Comment,
                PublicKey = generated.PublicKey,
                Fingerprint = generated.Fingerprint,
                CreatedUtc = DateTime.UtcNow,
                HasPassphrase = generated.HasPassphrase
            };

            await _vault.SaveAsync(record, generated.PrivateKeyPem).ConfigureAwait(true);

            LastPublicKey = generated.PublicKey;
            LastFingerprint = generated.Fingerprint;
            ResultMessage = L("Generate_Success");
            Passphrase = string.Empty;
            PassphraseConfirm = string.Empty;
            _log.Info(L("Log_Generated", generated.Algorithm, record.Name, record.Fingerprint));
            _setStatus(L("Status_KeyGenerated"));
            await _onKeyCreatedAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ResultMessage = ex.Message;
            _log.Error(ex.Message);
            _setStatus(L("Status_GenerateFailed"));
        }
        finally
        {
            IsBusy = false;
        }
    }
}
