using System.Collections.ObjectModel;
using SshKeyManager.ViewModels;

namespace SshKeyManager.Services.Ssh;

public interface ISshSessionWindowService
{
    ObservableCollection<ActiveSshSessionItemViewModel> ActiveSessions { get; }

    int SessionCount { get; }

    event EventHandler? SessionsChanged;

    Task OpenSessionAsync(SshSessionLaunchRequest request);

    void FocusSession(Guid sessionId);

    Task DisconnectSessionAsync(Guid sessionId);

    Task DisconnectAllAsync();
}
