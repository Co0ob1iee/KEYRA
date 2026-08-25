using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SshKeyManager.ViewModels;

public partial class ActiveSshSessionItemViewModel : ObservableObject
{
    private readonly Func<Guid, Task> _disconnectAsync;
    private readonly Action<Guid> _focus;

    public ActiveSshSessionItemViewModel(
        Guid sessionId,
        string title,
        string hostSummary,
        Func<Guid, Task> disconnectAsync,
        Action<Guid> focus)
    {
        SessionId = sessionId;
        Title = title ?? string.Empty;
        HostSummary = hostSummary ?? string.Empty;
        _disconnectAsync = disconnectAsync ?? throw new ArgumentNullException(nameof(disconnectAsync));
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        StatusText = string.Empty;
    }

    public Guid SessionId { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _hostSummary;

    [ObservableProperty]
    private string _statusText;

    [ObservableProperty]
    private bool _isConnected;

    [RelayCommand]
    private void Focus() => _focus(SessionId);

    [RelayCommand]
    private async Task DisconnectAsync() => await _disconnectAsync(SessionId).ConfigureAwait(true);
}
