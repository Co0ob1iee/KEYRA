using CommunityToolkit.Mvvm.ComponentModel;
using SshKeyManager.Models;
using SshKeyManager.Services;

namespace SshKeyManager.ViewModels;

public partial class KeyListItemViewModel : ObservableObject
{
    private readonly SshKeyRecord _record;
    private ILocalizationService _localization;

    public KeyListItemViewModel(SshKeyRecord record, ILocalizationService localization)
    {
        _record = record ?? throw new ArgumentNullException(nameof(record));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public SshKeyRecord Record => _record;

    public Guid Id => _record.Id;

    public string Name => _record.Name;

    public string AlgorithmLabel => _record.Algorithm switch
    {
        SshKeyAlgorithm.Ed25519 => _localization.GetString("Algorithm_Ed25519"),
        SshKeyAlgorithm.Rsa4096 => _localization.GetString("Algorithm_Rsa4096"),
        SshKeyAlgorithm.EcdsaP384 => _localization.GetString("Algorithm_EcdsaP384"),
        SshKeyAlgorithm.SkEd25519 => _localization.GetString("Algorithm_SkEd25519"),
        _ => _record.Algorithm.ToString()
    };

    public string Fingerprint => _record.Fingerprint;

    public string Comment => _record.Comment;

    public string CreatedLocal => _localization.FormatDateTimeUtc(_record.CreatedUtc);

    public bool HasPassphrase => _record.HasPassphrase;

    public string PublicKey => _record.PublicKey;

    public string PassphraseLabel => HasPassphrase
        ? _localization.GetString("Keys_PassphraseYes")
        : _localization.GetString("Keys_PassphraseNo");

    public void RefreshLocalization(ILocalizationService localization)
    {
        _localization = localization;
        OnPropertyChanged(string.Empty);
    }
}
