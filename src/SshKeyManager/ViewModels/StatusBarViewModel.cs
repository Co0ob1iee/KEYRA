using System.Reflection;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SshKeyManager.Services;
using SshKeyManager.Services.Ssh;

namespace SshKeyManager.ViewModels;

public partial class StatusBarViewModel : LocalizedViewModelBase
{
    private readonly ISshSessionWindowService _sessions;

    public StatusBarViewModel(
        ILocalizationService localization,
        ISshSessionWindowService sessions)
        : base(localization)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _sessions.SessionsChanged += OnSessionsChanged;
        AppVersion = ResolveVersion();
        RefreshConnectionLabel();
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _sectionLabel = string.Empty;

    [ObservableProperty]
    private string _connectionLabel = string.Empty;

    [ObservableProperty]
    private string _keyCountLabel = string.Empty;

    [ObservableProperty]
    private string _vaultStateLabel = string.Empty;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private Brush _connectionBrush = Brushes.Gray;

    public string ReadyLabel => L("StatusBar_Ready");

    public string KeysCountPrefix => L("StatusBar_Keys");

    public string SshPrefix => L("StatusBar_Ssh");

    public string VaultUnlockedLabel => L("StatusBar_VaultUnlocked");

    protected override void OnLocalizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnLocalizationPropertyChanged(sender, e);
        OnPropertyChanged(nameof(ReadyLabel));
        OnPropertyChanged(nameof(KeysCountPrefix));
        OnPropertyChanged(nameof(SshPrefix));
        OnPropertyChanged(nameof(VaultUnlockedLabel));
        RefreshConnectionLabel();
        VaultStateLabel = VaultUnlockedLabel;
    }

    public void SetStatus(string message) => StatusMessage = message;

    public void SetSection(string sectionName) => SectionLabel = sectionName;

    public void SetKeyCount(int count) => KeyCountLabel = L("StatusBar_KeyCount", count);

    public void SetVaultUnlocked() => VaultStateLabel = VaultUnlockedLabel;

    private void OnSessionsChanged(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(RefreshConnectionLabel);
            return;
        }

        RefreshConnectionLabel();
    }

    private void RefreshConnectionLabel()
    {
        var count = _sessions.SessionCount;
        ConnectionLabel = count switch
        {
            0 => L("Connections_Disconnected"),
            1 => L("StatusBar_SshOneSession"),
            _ => L("StatusBar_SshSessions", count)
        };

        ConnectionBrush = count > 0
            ? ResolveBrush("SuccessBrush", Brushes.LimeGreen)
            : ResolveBrush("TextSecondaryBrush", Brushes.Gray);
    }

    private static Brush ResolveBrush(string resourceKey, Brush fallback)
    {
        try
        {
            if (System.Windows.Application.Current?.TryFindResource(resourceKey) is Brush brush)
            {
                return brush;
            }
        }
        catch (Exception)
        {
            // Fall through to fallback brush.
        }

        return fallback;
    }

    private static string ResolveVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                // Strip optional +build metadata from InformationalVersion.
                var semVer = informational.Split('+', 2)[0].Trim();
                if (semVer.Length > 0)
                {
                    return $"KEYRA v{semVer}";
                }
            }

            var version = assembly.GetName().Version;
            if (version is not null)
            {
                return $"KEYRA v{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
            }
        }
        catch (Exception)
        {
            // Fall through.
        }

        return "KEYRA";
    }
}
