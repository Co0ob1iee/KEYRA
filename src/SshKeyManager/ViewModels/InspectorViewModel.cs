using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Models;
using SshKeyManager.Presentation;
using SshKeyManager.Services;

namespace SshKeyManager.ViewModels;

public partial class InspectorViewModel : LocalizedViewModelBase
{
    private readonly IKeyInspectorTarget _keys;

    public InspectorViewModel(ILocalizationService localization, IKeyInspectorTarget keys)
        : base(localization)
    {
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        if (_keys is ObservableObject observable)
        {
            observable.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(IKeyInspectorTarget.SelectedKey)
                    or nameof(IKeyInspectorTarget.IsPrivateKeyVisible)
                    or nameof(IKeyInspectorTarget.RevealedPrivateKey)
                    or nameof(IKeyInspectorTarget.RevealCountdownText))
                {
                    RefreshSelectionState();
                }
            };
        }
    }

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private SshKeyRecord? _selectedRecord;

    public IKeyInspectorTarget Keys => _keys;

    public bool HasSelection => SelectedRecord is not null;

    public string Title => L("Inspector_Title");

    public string TabDetails => L("Inspector_TabDetails");

    public string TabActions => L("Inspector_TabActions");

    public string SelectKey => L("Inspector_SelectKey");

    public string NameLabel => L("Inspector_Name");

    public string AlgorithmLabel => L("Inspector_Algorithm");

    public string FingerprintLabel => L("Inspector_Fingerprint");

    public string CommentLabel => L("Inspector_Comment");

    public string PublicKeyLabel => L("Inspector_PublicKey");

    public string VaultPathLabel => L("Inspector_VaultPath");

    public string CopyPublicKeyLabel => L("Inspector_CopyPublicKey");

    public string ShowPrivateKeyLabel => L("Inspector_ShowPrivateKey");

    public string CopyPrivateKeyLabel => L("Inspector_CopyPrivateKey");

    public string ExportFolderLabel => L("Inspector_ExportFolder");

    public string ExportSshLabel => L("Inspector_ExportSsh");

    public string DeleteLabel => L("Inspector_Delete");

    public string PrivateKeyLabel => L("Inspector_PrivateKey");

    public string HideNowLabel => L("Inspector_HideNow");

    public string VaultPathDisplay =>
        SelectedRecord is null
            ? string.Empty
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SshKeyManager",
                "vault",
                $"{SelectedRecord.Id:N}.key.enc");

    public string AlgorithmDisplay => SelectedRecord?.Algorithm switch
    {
        SshKeyAlgorithm.Ed25519 => L("Algorithm_Ed25519"),
        SshKeyAlgorithm.Rsa4096 => L("Algorithm_Rsa4096"),
        null => string.Empty,
        _ => SelectedRecord!.Algorithm.ToString()
    };

    public bool IsPrivateKeyVisible => _keys.IsPrivateKeyVisible;

    public string RevealedPrivateKey => _keys.RevealedPrivateKey;

    public string RevealCountdownText => _keys.RevealCountdownText;

    protected override void OnLocalizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnLocalizationPropertyChanged(sender, e);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(TabDetails));
        OnPropertyChanged(nameof(TabActions));
        OnPropertyChanged(nameof(SelectKey));
        OnPropertyChanged(nameof(NameLabel));
        OnPropertyChanged(nameof(AlgorithmLabel));
        OnPropertyChanged(nameof(FingerprintLabel));
        OnPropertyChanged(nameof(CommentLabel));
        OnPropertyChanged(nameof(PublicKeyLabel));
        OnPropertyChanged(nameof(VaultPathLabel));
        OnPropertyChanged(nameof(CopyPublicKeyLabel));
        OnPropertyChanged(nameof(ShowPrivateKeyLabel));
        OnPropertyChanged(nameof(CopyPrivateKeyLabel));
        OnPropertyChanged(nameof(ExportFolderLabel));
        OnPropertyChanged(nameof(ExportSshLabel));
        OnPropertyChanged(nameof(DeleteLabel));
        OnPropertyChanged(nameof(PrivateKeyLabel));
        OnPropertyChanged(nameof(HideNowLabel));
        OnPropertyChanged(nameof(AlgorithmDisplay));
    }

    public void SetVisible(bool visible) => IsVisible = visible;

    public void SetSelectedRecord(SshKeyRecord? record)
    {
        SelectedRecord = record;
        RefreshSelectionState();
    }

    partial void OnSelectedRecordChanged(SshKeyRecord? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(VaultPathDisplay));
        OnPropertyChanged(nameof(AlgorithmDisplay));
    }

    private void RefreshSelectionState()
    {
        SelectedRecord = _keys.SelectedKey?.Record;
        OnPropertyChanged(nameof(IsPrivateKeyVisible));
        OnPropertyChanged(nameof(RevealedPrivateKey));
        OnPropertyChanged(nameof(RevealCountdownText));
    }

    [RelayCommand]
    private void CopyPublicKey() => _keys.RequestCopyPublicKey();

    [RelayCommand]
    private void RevealPrivateKey() => _keys.RequestRevealPrivateKey();

    [RelayCommand]
    private void CopyPrivateKey() => _keys.RequestCopyPrivateKey();

    [RelayCommand]
    private void ExportFolder() => _keys.RequestExportFolder();

    [RelayCommand]
    private void ExportToSsh() => _keys.RequestExportToSsh();

    [RelayCommand]
    private void DeleteKey() => _keys.RequestDeleteKey();

    [RelayCommand]
    private void HidePrivateKey() => _keys.RequestHidePrivateKey();
}
