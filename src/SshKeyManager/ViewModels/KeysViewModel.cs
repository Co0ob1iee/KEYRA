using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Models;
using SshKeyManager.Presentation;
using SshKeyManager.Services;

namespace SshKeyManager.ViewModels;

public partial class KeysViewModel : LocalizedViewModelBase, IKeyInspectorTarget
{
    private readonly IVaultStore _vault;
    private readonly IKeyExportService _export;
    private readonly IClipboardService _clipboard;
    private readonly IAppLogService _log;
    private readonly IDialogService _dialogs;
    private Action<string> _setStatus = _ => { };
    private Action<SshKeyRecord?> _onSelectionChanged = _ => { };
    private Action<int> _onKeysChanged = _ => { };
    private CancellationTokenSource? _revealCts;

    public KeysViewModel(
        IVaultStore vault,
        IKeyExportService export,
        IClipboardService clipboard,
        IAppLogService log,
        ILocalizationService localization,
        IDialogService dialogs)
        : base(localization)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    public void ConfigureShell(
        Action<string> setStatus,
        Action<SshKeyRecord?> onSelectionChanged,
        Action<int> onKeysChanged)
    {
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _onSelectionChanged = onSelectionChanged ?? throw new ArgumentNullException(nameof(onSelectionChanged));
        _onKeysChanged = onKeysChanged ?? throw new ArgumentNullException(nameof(onKeysChanged));
    }

    public ObservableCollection<KeyListItemViewModel> Keys { get; } = new();

    public bool HasKeys => Keys.Count > 0;

    public string Title => L("Keys_Title");

    public string RefreshLabel => L("Keys_Refresh");

    public string SearchWatermark => L("Keys_SearchWatermark");

    public string EmptyMessage => L("Keys_Empty");

    public string CopyPubLabel => L("Keys_CopyPub");

    public string ExportLabel => L("Keys_Export");

    public string DeleteLabel => L("Keys_Delete");

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private KeyListItemViewModel? _selectedKey;

    [ObservableProperty]
    private bool _isPrivateKeyVisible;

    [ObservableProperty]
    private string _revealedPrivateKey = string.Empty;

    [ObservableProperty]
    private string _revealCountdownText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public IEnumerable<KeyListItemViewModel> FilteredKeys
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return Keys;
            }

            var q = SearchText.Trim();
            return Keys.Where(k =>
                k.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                k.Fingerprint.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                k.Comment.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                k.AlgorithmLabel.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
    }

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredKeys));

    partial void OnSelectedKeyChanged(KeyListItemViewModel? value)
    {
        HidePrivateKey();
        _onSelectionChanged(value?.Record);
        OnPropertyChanged(nameof(FilteredKeys));
    }

    protected override void OnLocalizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnLocalizationPropertyChanged(sender, e);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(RefreshLabel));
        OnPropertyChanged(nameof(SearchWatermark));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(CopyPubLabel));
        OnPropertyChanged(nameof(ExportLabel));
        OnPropertyChanged(nameof(DeleteLabel));
        foreach (var key in Keys)
        {
            key.RefreshLocalization(Localization);
        }
    }

    [RelayCommand]
    private void SelectKey(KeyListItemViewModel? item)
    {
        if (item is not null)
        {
            SelectedKey = item;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var records = await _vault.ListAsync().ConfigureAwait(true);
            Keys.Clear();
            foreach (var record in records)
            {
                Keys.Add(new KeyListItemViewModel(record, Localization));
            }

            OnPropertyChanged(nameof(FilteredKeys));
            OnPropertyChanged(nameof(HasKeys));
            _onKeysChanged(Keys.Count);
            _setStatus(L("Status_KeysLoaded", Keys.Count));
            _log.Info(L("Log_VaultRefreshed", Keys.Count));
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
            _setStatus(L("Status_FailedLoadVault"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CopyPublicKey(KeyListItemViewModel? item)
    {
        item ??= SelectedKey;
        if (item is null)
        {
            return;
        }

        try
        {
            _clipboard.SetText(item.PublicKey, isPrivateKey: false);
            _log.Info(L("Log_CopiedPublic", item.Name));
            _setStatus(L("Status_PublicKeyCopied"));
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
            _setStatus(L("Status_CopyFailed"));
        }
    }

    [RelayCommand]
    private async Task CopyPrivateKeyAsync()
    {
        if (SelectedKey is null)
        {
            return;
        }

        if (!_dialogs.Confirm(L("Dialog_CopyPrivateKey"), L("Dialog_Title"), isWarning: true))
        {
            return;
        }

        try
        {
            using var material = await _vault.LoadPrivateKeyAsync(SelectedKey.Id).ConfigureAwait(true);
            _clipboard.SetText(material.GetPrivateKeyPem(), isPrivateKey: true);
            _log.Warning(L("Log_CopiedPrivate", SelectedKey.Name));
            _setStatus(L("Status_PrivateKeyCopied"));
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
            _setStatus(L("Status_CopyPrivateKeyFailed"));
        }
    }

    [RelayCommand]
    private async Task RevealPrivateKeyAsync()
    {
        if (SelectedKey is null)
        {
            return;
        }

        if (!_dialogs.Confirm(L("Dialog_RevealPrivateKey"), L("Dialog_Title"), isWarning: true))
        {
            return;
        }

        try
        {
            using var material = await _vault.LoadPrivateKeyAsync(SelectedKey.Id).ConfigureAwait(true);
            RevealedPrivateKey = material.GetPrivateKeyPem();
            IsPrivateKeyVisible = true;
            _log.Warning(L("Log_RevealedPrivate", SelectedKey.Name));
            _setStatus(L("Status_PrivateKeyVisible"));
            await StartRevealCountdownAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
            _setStatus(L("Status_RevealFailed"));
            HidePrivateKey();
        }
    }

    [RelayCommand]
    private void HidePrivateKey()
    {
        _revealCts?.Cancel();
        _revealCts = null;
        RevealedPrivateKey = string.Empty;
        IsPrivateKeyVisible = false;
        RevealCountdownText = string.Empty;
    }

    [RelayCommand]
    private async Task ExportAsync(KeyListItemViewModel? item)
    {
        item ??= SelectedKey;
        if (item is null)
        {
            return;
        }

        var folder = _dialogs.PickFolder(L("Dialog_ExportFolderTitle"));
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        try
        {
            using var material = await _vault.LoadPrivateKeyAsync(item.Id).ConfigureAwait(true);
            var overwrite = false;
            try
            {
                await _export.ExportToFolderAsync(item.Record, material, folder, overwrite: false)
                    .ConfigureAwait(true);
            }
            catch (IOException)
            {
                if (!_dialogs.Confirm(L("Dialog_ExportOverwrite"), L("Dialog_Title")))
                {
                    return;
                }

                overwrite = true;
                await _export.ExportToFolderAsync(item.Record, material, folder, overwrite)
                    .ConfigureAwait(true);
            }

            _log.Info(L("Log_Exported", item.Name, folder));
            _setStatus(L("Status_KeyExported"));
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
            _setStatus(L("Status_ExportFailed"));
        }
    }

    [RelayCommand]
    private async Task ExportToSshAsync()
    {
        if (SelectedKey is null)
        {
            return;
        }

        var sshDir = _export.GetDefaultSshDirectory();
        if (!_dialogs.Confirm(L("Dialog_ExportToSsh", SelectedKey.Name, sshDir), L("Dialog_Title")))
        {
            return;
        }

        try
        {
            using var material = await _vault.LoadPrivateKeyAsync(SelectedKey.Id).ConfigureAwait(true);
            try
            {
                await _export.ExportToUserSshAsync(SelectedKey.Record, material, overwrite: false)
                    .ConfigureAwait(true);
            }
            catch (IOException)
            {
                if (!_dialogs.Confirm(L("Dialog_SshOverwrite"), L("Dialog_Title"), isWarning: true))
                {
                    return;
                }

                await _export.ExportToUserSshAsync(SelectedKey.Record, material, overwrite: true)
                    .ConfigureAwait(true);
            }

            _log.Info(L("Log_ExportedSsh", SelectedKey.Name));
            _setStatus(L("Status_ExportedToSsh"));
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
            _setStatus(L("Status_ExportToSshFailed"));
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(KeyListItemViewModel? item)
    {
        item ??= SelectedKey;
        if (item is null)
        {
            return;
        }

        if (!_dialogs.Confirm(L("Dialog_DeleteKey", item.Name), L("Dialog_Title"), isWarning: true))
        {
            return;
        }

        try
        {
            HidePrivateKey();
            await _vault.DeleteAsync(item.Id).ConfigureAwait(true);
            _log.Info(L("Log_Deleted", item.Name));
            _setStatus(L("Status_KeyDeleted"));
            if (ReferenceEquals(SelectedKey, item))
            {
                SelectedKey = null;
            }

            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
            _setStatus(L("Status_DeleteFailed"));
        }
    }

    public void SelectById(Guid id)
    {
        SelectedKey = Keys.FirstOrDefault(k => k.Id == id);
    }

    public void RequestCopyPublicKey() => CopyPublicKeyCommand.Execute(SelectedKey);

    public void RequestRevealPrivateKey() => RevealPrivateKeyCommand.Execute(null);

    public void RequestCopyPrivateKey() => CopyPrivateKeyCommand.Execute(null);

    public void RequestExportFolder() => ExportCommand.Execute(SelectedKey);

    public void RequestExportToSsh() => ExportToSshCommand.Execute(null);

    public void RequestDeleteKey() => DeleteCommand.Execute(SelectedKey);

    public void RequestHidePrivateKey() => HidePrivateKeyCommand.Execute(null);

    private async Task StartRevealCountdownAsync()
    {
        _revealCts?.Cancel();
        _revealCts = new CancellationTokenSource();
        var token = _revealCts.Token;
        const int seconds = 15;
        try
        {
            for (var remaining = seconds; remaining >= 0; remaining--)
            {
                token.ThrowIfCancellationRequested();
                RevealCountdownText = remaining == 0 ? string.Empty : L("Reveal_HidingIn", remaining);
                if (remaining == 0)
                {
                    break;
                }

                await Task.Delay(1000, token).ConfigureAwait(true);
            }

            if (!token.IsCancellationRequested)
            {
                HidePrivateKey();
                _setStatus(L("Status_PrivateKeyHidden"));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when hidden early.
        }
    }
}
