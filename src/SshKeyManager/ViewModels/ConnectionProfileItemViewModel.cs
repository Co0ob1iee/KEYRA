using CommunityToolkit.Mvvm.ComponentModel;
using SshKeyManager.Models;
using SshKeyManager.Services;

namespace SshKeyManager.ViewModels;

public sealed partial class ConnectionProfileItemViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;

    public ConnectionProfileItemViewModel(SshConnectionProfile profile, ILocalizationService localization)
    {
        Profile = profile?.Clone() ?? throw new ArgumentNullException(nameof(profile));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public SshConnectionProfile Profile { get; }

    public Guid Id => Profile.Id;

    public string Name => Profile.Name;

    public string HostPort => $"{Profile.Host}:{Profile.Port}";

    public string Username => Profile.Username;

    public bool IsFavorite => Profile.IsFavorite;

    public string FavoriteGlyph => Profile.IsFavorite ? "\uE735" : "\uE734";

    public string LastUsedText =>
        Profile.LastConnectedUtc is DateTime utc
            ? _localization.GetString("Connections_LastUsed", _localization.FormatDateTimeUtc(utc))
            : _localization.GetString("Connections_NeverConnected");

    public void Apply(SshConnectionProfile source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Profile.Id = source.Id;
        Profile.Name = source.Name;
        Profile.Host = source.Host;
        Profile.Port = source.Port;
        Profile.Username = source.Username;
        Profile.AuthMode = source.AuthMode;
        Profile.VaultKeyId = source.VaultKeyId;
        Profile.LastConnectedUtc = source.LastConnectedUtc;
        Profile.IsFavorite = source.IsFavorite;
        NotifyDisplayChanged();
    }

    public void NotifyDisplayChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(HostPort));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(FavoriteGlyph));
        OnPropertyChanged(nameof(LastUsedText));
    }
}
