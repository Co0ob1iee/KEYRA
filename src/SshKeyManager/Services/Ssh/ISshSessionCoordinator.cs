using System.ComponentModel;
using SshKeyManager.Models;

namespace SshKeyManager.Services.Ssh;

public interface ISshSessionCoordinator : INotifyPropertyChanged
{
    string SessionOutput { get; }

    bool IsConnected { get; }

    bool IsBusy { get; }

    string ConnectionStatus { get; set; }

    CancellationToken SessionToken { get; }

    void ConfigureShell(Action<string> setStatus);

    Task ConnectAsync(
        string host,
        int port,
        string username,
        bool usePasswordAuth,
        string password,
        SshKeyRecord? selectedKey,
        string keyPassphrase,
        Guid? auditServerId = null,
        SshConnectionProfile? jumpHost = null,
        SshKeyRecord? jumpHostKey = null,
        string jumpHostKeyPassphrase = "");

    Task DisconnectAsync();

    Task SendRawCommandAsync(string command);

    void ClearOutput();

    void AppendOutput(string text, bool isError = false);

    void RefreshConnectionStatusLabel(SshConnectionState? state = null);
}
