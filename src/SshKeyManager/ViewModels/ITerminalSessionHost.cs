using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Services.Ssh;

namespace SshKeyManager.ViewModels;

/// <summary>
/// Host for <see cref="Views.Controls.TerminalInputControl"/> — session window or any terminal surface.
/// </summary>
public interface ITerminalSessionHost
{
    ITerminalInputController Terminal { get; }

    bool IsConnected { get; }

    IAsyncRelayCommand SendCommandCommand { get; }
}
