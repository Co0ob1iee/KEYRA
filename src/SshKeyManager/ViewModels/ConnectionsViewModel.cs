using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Models;
using SshKeyManager.Presentation;
using SshKeyManager.Services;
using SshKeyManager.Services.Ssh;

namespace SshKeyManager.ViewModels;

public partial class ConnectionsViewModel : LocalizedViewModelBase
{
    private static readonly string[] LocalizedPropertyNames =
    [
        nameof(Title), nameof(HostLabel), nameof(PortLabel), nameof(UsernameLabel),
        nameof(PasswordAuthLabel), nameof(SshPasswordLabel), nameof(VaultKeyLabel),
        nameof(KeyPassphraseLabel), nameof(StatusLabel), nameof(ConnectLabel),
        nameof(DisconnectLabel), nameof(ProfilesLabel), nameof(ProfileNameLabel),
        nameof(NewProfileLabel), nameof(SaveProfileLabel), nameof(SaveAsProfileLabel),
        nameof(DeleteProfileLabel), nameof(FavoriteLabel), nameof(EmptyProfilesTitle),
        nameof(EmptyProfilesMessage), nameof(PasswordNotSavedHint),
        nameof(ActiveSessionsLabel), nameof(EmptySessionsTitle), nameof(EmptySessionsMessage),
        nameof(FocusSessionLabel), nameof(ActiveSessionCountLabel),
        nameof(JumpHostLabel), nameof(JumpHostNoneLabel), nameof(JumpHostHint)
    ];

    private readonly ISshSessionWindowService _sessions;
    private readonly IVaultStore _vault;
    private readonly ISshConnectionProfileStore _profiles;
    private readonly IDialogService _dialogs;
    private readonly IAppLogService _log;
    private Action<string> _setStatus = _ => { };
    private bool _suppressProfileSelection;

    public ConnectionsViewModel(
        ISshSessionWindowService sessions,
        IVaultStore vault,
        ISshConnectionProfileStore profiles,
        IDialogService dialogs,
        IAppLogService log,
        ILocalizationService localization)
        : base(localization)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        _sessions.SessionsChanged += OnSessionsChanged;
        ((INotifyCollectionChanged)_sessions.ActiveSessions).CollectionChanged += OnActiveSessionsCollectionChanged;
    }

    public ObservableCollection<SshKeyRecord> AvailableKeys { get; } = new();

    public ObservableCollection<ConnectionProfileItemViewModel> SavedProfiles { get; } = new();

    public ObservableCollection<ActiveSshSessionItemViewModel> ActiveSessions => _sessions.ActiveSessions;

    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private int _port = 22;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private SshKeyRecord? _selectedKey;
    [ObservableProperty] private bool _usePasswordAuth;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _keyPassphrase = string.Empty;
    [ObservableProperty] private string _profileName = string.Empty;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private ConnectionProfileItemViewModel? _selectedProfile;
    [ObservableProperty] private ConnectionProfileItemViewModel? _selectedJumpHost;
    [ObservableProperty] private bool _hasProfiles;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ActiveSshSessionItemViewModel? _selectedActiveSession;

    public bool HasActiveSessions => ActiveSessions.Count > 0;

    public string Title => L("Connections_Title");
    public string HostLabel => L("Connections_Host");
    public string PortLabel => L("Connections_Port");
    public string UsernameLabel => L("Connections_Username");
    public string PasswordAuthLabel => L("Connections_PasswordAuth");
    public string SshPasswordLabel => L("Connections_SshPassword");
    public string VaultKeyLabel => L("Connections_VaultKey");
    public string KeyPassphraseLabel => L("Connections_KeyPassphrase");
    public string StatusLabel => L("Connections_Status");
    public string ConnectLabel => L("Connections_Connect");
    public string DisconnectLabel => L("Connections_Disconnect");
    public string ProfilesLabel => L("Connections_Profiles");
    public string ProfileNameLabel => L("Connections_ProfileName");
    public string NewProfileLabel => L("Connections_NewProfile");
    public string SaveProfileLabel => L("Connections_SaveProfile");
    public string SaveAsProfileLabel => L("Connections_SaveAsProfile");
    public string DeleteProfileLabel => L("Connections_DeleteProfile");
    public string FavoriteLabel => L("Connections_Favorite");
    public string EmptyProfilesTitle => L("Connections_EmptyProfilesTitle");
    public string EmptyProfilesMessage => L("Connections_EmptyProfilesMessage");
    public string PasswordNotSavedHint => L("Connections_PasswordNotSavedHint");
    public string ActiveSessionsLabel => L("Connections_ActiveSessions");
    public string EmptySessionsTitle => L("Connections_EmptySessionsTitle");
    public string EmptySessionsMessage => L("Connections_EmptySessionsMessage");
    public string FocusSessionLabel => L("Connections_FocusSession");
    public string ActiveSessionCountLabel => L("Connections_ActiveSessionCount", ActiveSessions.Count);
    public string JumpHostLabel => L("Connections_JumpHost");
    public string JumpHostNoneLabel => L("Connections_JumpHostNone");
    public string JumpHostHint => L("Connections_JumpHostHint");

    public IEnumerable<ConnectionProfileItemViewModel?> JumpHostChoices
    {
        get
        {
            yield return null;
            foreach (var profile in SavedProfiles)
            {
                if (SelectedProfile is null || profile.Id != SelectedProfile.Id)
                {
                    yield return profile;
                }
            }
        }
    }

    public void ConfigureShell(Action<string> setStatus)
    {
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
    }

    protected override void OnLocalizationChanged(string key)
    {
        foreach (var name in LocalizedPropertyNames)
        {
            OnPropertyChanged(name);
        }

        foreach (var item in SavedProfiles)
        {
            item.NotifyDisplayChanged();
        }
    }

    public async Task LoadKeysAsync()
    {
        try
        {
            var keys = await _vault.ListAsync().ConfigureAwait(true);
            AvailableKeys.Clear();
            foreach (var key in keys)
            {
                AvailableKeys.Add(key);
            }

            SelectedKey ??= AvailableKeys.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load keys for connections: {ex.Message}");
        }
    }

    public async Task LoadProfilesAsync()
    {
        try
        {
            var selectedId = SelectedProfile?.Id;
            var list = await _profiles.ListAsync().ConfigureAwait(true);

            _suppressProfileSelection = true;
            try
            {
                SavedProfiles.Clear();
                foreach (var profile in list)
                {
                    SavedProfiles.Add(new ConnectionProfileItemViewModel(profile, Localization));
                }

                HasProfiles = SavedProfiles.Count > 0;

                if (selectedId is Guid id)
                {
                    SelectedProfile = SavedProfiles.FirstOrDefault(p => p.Id == id);
                }

                OnPropertyChanged(nameof(JumpHostChoices));
                if (SelectedJumpHost is not null)
                {
                    SelectedJumpHost = SavedProfiles.FirstOrDefault(p => p.Id == SelectedJumpHost.Id);
                }
            }
            finally
            {
                _suppressProfileSelection = false;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load connection profiles: {ex.Message}");
            HasProfiles = SavedProfiles.Count > 0;
        }
    }

    partial void OnSelectedProfileChanged(ConnectionProfileItemViewModel? value)
    {
        if (_suppressProfileSelection || value is null)
        {
            return;
        }

        ApplyProfileToForm(value.Profile);
        DeleteProfileCommand.NotifyCanExecuteChanged();
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void NewProfile()
    {
        _suppressProfileSelection = true;
        try
        {
            SelectedProfile = null;
        }
        finally
        {
            _suppressProfileSelection = false;
        }

        ClearFormForNewProfile();
        DeleteProfileCommand.NotifyCanExecuteChanged();
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (!TryBuildProfileFromForm(SelectedProfile?.Id ?? Guid.Empty, out var profile, out var error))
        {
            _dialogs.ShowError(error, L("Dialog_Title"));
            return;
        }

        try
        {
            var saved = await _profiles.UpsertAsync(profile).ConfigureAwait(true);
            await LoadProfilesAsync().ConfigureAwait(true);
            SelectedProfile = SavedProfiles.FirstOrDefault(p => p.Id == saved.Id);
            _log.Info(L("Log_ProfileSaved", saved.Name));
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to save connection profile: {ex.Message}");
            _dialogs.ShowError(L("Connections_ErrSaveProfile", ex.Message), L("Dialog_Title"));
        }
    }

    [RelayCommand]
    private async Task SaveAsProfileAsync()
    {
        if (!TryBuildProfileFromForm(Guid.Empty, out var profile, out var error))
        {
            _dialogs.ShowError(error, L("Dialog_Title"));
            return;
        }

        try
        {
            var saved = await _profiles.UpsertAsync(profile).ConfigureAwait(true);
            await LoadProfilesAsync().ConfigureAwait(true);
            SelectedProfile = SavedProfiles.FirstOrDefault(p => p.Id == saved.Id);
            _log.Info(L("Log_ProfileSaved", saved.Name));
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to save connection profile: {ex.Message}");
            _dialogs.ShowError(L("Connections_ErrSaveProfile", ex.Message), L("Dialog_Title"));
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteOrFavoriteProfile))]
    private async Task DeleteProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var name = SelectedProfile.Name;
        if (!_dialogs.Confirm(L("Dialog_DeleteProfile", name), L("Dialog_Title"), isWarning: true))
        {
            return;
        }

        try
        {
            var id = SelectedProfile.Id;
            await _profiles.DeleteAsync(id).ConfigureAwait(true);
            await LoadProfilesAsync().ConfigureAwait(true);
            NewProfile();
            _log.Info(L("Log_ProfileDeleted", name));
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to delete connection profile: {ex.Message}");
            _dialogs.ShowError(L("Connections_ErrDeleteProfile", ex.Message), L("Dialog_Title"));
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteOrFavoriteProfile))]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            var updated = SelectedProfile.Profile.Clone();
            updated.IsFavorite = !updated.IsFavorite;
            IsFavorite = updated.IsFavorite;
            var saved = await _profiles.UpsertAsync(updated).ConfigureAwait(true);
            await LoadProfilesAsync().ConfigureAwait(true);
            SelectedProfile = SavedProfiles.FirstOrDefault(p => p.Id == saved.Id);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to update favorite: {ex.Message}");
            _dialogs.ShowError(L("Connections_ErrSaveProfile", ex.Message), L("Dialog_Title"));
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (UsePasswordAuth && string.IsNullOrEmpty(Password))
        {
            _dialogs.ShowError(L("Connections_ErrPasswordRequired"), L("Dialog_Title"));
            return;
        }

        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Username))
        {
            _dialogs.ShowError(L("Connections_ErrHostUser"), L("Dialog_Title"));
            return;
        }

        if (!UsePasswordAuth && SelectedKey is null)
        {
            _dialogs.ShowError(L("Connections_ErrSelectKey"), L("Dialog_Title"));
            return;
        }

        if (!UsePasswordAuth && SelectedKey?.Algorithm == SshKeyAlgorithm.SkEd25519)
        {
            _dialogs.ShowError(L("Connections_ErrSkKeyNotSupported"), L("Dialog_Title"));
            return;
        }

        IsBusy = true;
        ConnectCommand.NotifyCanExecuteChanged();
        try
        {
            var profileName = string.IsNullOrWhiteSpace(ProfileName) ? null : ProfileName.Trim();
            SshConnectionProfile? jump = null;
            SshKeyRecord? jumpKey = null;
            if (SelectedJumpHost is not null)
            {
                jump = SelectedJumpHost.Profile.Clone();
                if (jump.VaultKeyId is Guid jumpKeyId)
                {
                    jumpKey = AvailableKeys.FirstOrDefault(k => k.Id == jumpKeyId);
                }

                if (jumpKey is null)
                {
                    _dialogs.ShowError(L("Connections_ErrJumpKey"), L("Dialog_Title"));
                    return;
                }

                if (jumpKey.Algorithm == SshKeyAlgorithm.SkEd25519)
                {
                    _dialogs.ShowError(L("Connections_ErrSkKeyNotSupported"), L("Dialog_Title"));
                    return;
                }
            }

            var request = new SshSessionLaunchRequest
            {
                ProfileId = SelectedProfile?.Id,
                ProfileName = profileName,
                Host = Host.Trim(),
                Port = Port,
                Username = Username.Trim(),
                UsePasswordAuth = UsePasswordAuth,
                Password = Password,
                SelectedKey = SelectedKey,
                KeyPassphrase = KeyPassphrase,
                JumpHost = jump,
                JumpHostKey = jumpKey
            };

            await _sessions.OpenSessionAsync(request).ConfigureAwait(true);
            _setStatus(L("Connections_SessionOpened", request.BuildDisplayTitle()));

            if (SelectedProfile is not null)
            {
                try
                {
                    var updated = SelectedProfile.Profile.Clone();
                    updated.LastConnectedUtc = DateTime.UtcNow;
                    var saved = await _profiles.UpsertAsync(updated).ConfigureAwait(true);
                    await LoadProfilesAsync().ConfigureAwait(true);
                    SelectedProfile = SavedProfiles.FirstOrDefault(p => p.Id == saved.Id);
                }
                catch (Exception ex)
                {
                    _log.Error($"Failed to update last-connected for profile: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to open SSH session: {ex.Message}");
            _dialogs.ShowError(L("Connections_ErrOpenSession", ex.Message), L("Dialog_Title"));
        }
        finally
        {
            IsBusy = false;
            ConnectCommand.NotifyCanExecuteChanged();
            RefreshActiveSessionLabels();
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnectSelected))]
    private async Task DisconnectSelectedAsync()
    {
        if (SelectedActiveSession is null)
        {
            return;
        }

        await _sessions.DisconnectSessionAsync(SelectedActiveSession.SessionId).ConfigureAwait(true);
        RefreshActiveSessionLabels();
    }

    [RelayCommand(CanExecute = nameof(CanDisconnectAll))]
    private async Task DisconnectAllAsync()
    {
        await _sessions.DisconnectAllAsync().ConfigureAwait(true);
        RefreshActiveSessionLabels();
    }

    [RelayCommand(CanExecute = nameof(CanFocusSelected))]
    private void FocusSelected()
    {
        if (SelectedActiveSession is null)
        {
            return;
        }

        _sessions.FocusSession(SelectedActiveSession.SessionId);
    }

    private bool CanConnect() => !IsBusy;

    private bool CanDisconnectSelected() => !IsBusy && SelectedActiveSession is not null;

    private bool CanDisconnectAll() => !IsBusy && HasActiveSessions;

    private bool CanFocusSelected() => SelectedActiveSession is not null;

    private bool CanDeleteOrFavoriteProfile() => SelectedProfile is not null;

    partial void OnSelectedActiveSessionChanged(ActiveSshSessionItemViewModel? value)
    {
        DisconnectSelectedCommand.NotifyCanExecuteChanged();
        FocusSelectedCommand.NotifyCanExecuteChanged();
    }

    private void ApplyProfileToForm(SshConnectionProfile profile)
    {
        ProfileName = profile.Name;
        Host = profile.Host;
        Port = profile.Port <= 0 ? 22 : profile.Port;
        Username = profile.Username;
        UsePasswordAuth = profile.AuthMode == SshAuthMode.Password;
        IsFavorite = profile.IsFavorite;
        Password = string.Empty;
        KeyPassphrase = string.Empty;

        if (profile.ProxyJumpId is Guid jumpId)
        {
            SelectedJumpHost = SavedProfiles.FirstOrDefault(p => p.Id == jumpId);
        }
        else
        {
            SelectedJumpHost = null;
        }

        if (profile.VaultKeyId is Guid keyId)
        {
            SelectedKey = AvailableKeys.FirstOrDefault(k => k.Id == keyId)
                ?? AvailableKeys.FirstOrDefault();
        }
        else
        {
            SelectedKey = AvailableKeys.FirstOrDefault();
        }
    }

    private void ClearFormForNewProfile()
    {
        ProfileName = string.Empty;
        Host = string.Empty;
        Port = 22;
        Username = string.Empty;
        UsePasswordAuth = false;
        IsFavorite = false;
        Password = string.Empty;
        KeyPassphrase = string.Empty;
        SelectedJumpHost = null;
        SelectedKey = AvailableKeys.FirstOrDefault();
    }

    private bool TryBuildProfileFromForm(Guid id, out SshConnectionProfile profile, out string error)
    {
        profile = new SshConnectionProfile();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            error = L("Connections_ErrProfileName");
            return false;
        }

        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Username))
        {
            error = L("Connections_ErrHostUser");
            return false;
        }

        if (Port is < 1 or > 65535)
        {
            error = L("Connections_ErrPort");
            return false;
        }

        if (!UsePasswordAuth && SelectedKey is null)
        {
            error = L("Connections_ErrSelectKey");
            return false;
        }

        if (SelectedJumpHost is not null && SelectedJumpHost.Id == id && id != Guid.Empty)
        {
            error = L("Connections_ErrJumpSelf");
            return false;
        }

        if (SelectedJumpHost is not null && SelectedJumpHost.Profile.VaultKeyId is null)
        {
            error = L("Connections_ErrJumpKey");
            return false;
        }

        profile = new SshConnectionProfile
        {
            Id = id,
            Name = ProfileName.Trim(),
            Host = Host.Trim(),
            Port = Port,
            Username = Username.Trim(),
            AuthMode = UsePasswordAuth ? SshAuthMode.Password : SshAuthMode.Key,
            VaultKeyId = UsePasswordAuth ? null : SelectedKey?.Id,
            ProxyJumpId = SelectedJumpHost?.Id,
            IsFavorite = IsFavorite,
            LastConnectedUtc = SelectedProfile?.Profile.LastConnectedUtc
        };

        return true;
    }

    private void OnSessionsChanged(object? sender, EventArgs e) => RefreshActiveSessionLabels();

    private void OnActiveSessionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RefreshActiveSessionLabels();

    private void RefreshActiveSessionLabels()
    {
        OnPropertyChanged(nameof(HasActiveSessions));
        OnPropertyChanged(nameof(ActiveSessionCountLabel));
        DisconnectAllCommand.NotifyCanExecuteChanged();
        DisconnectSelectedCommand.NotifyCanExecuteChanged();
        FocusSelectedCommand.NotifyCanExecuteChanged();
    }
}
