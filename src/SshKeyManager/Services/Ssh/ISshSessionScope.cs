namespace SshKeyManager.Services.Ssh;

public interface ISshSessionScope : IDisposable
{
    Guid SessionId { get; }

    ISshConnectionService Connection { get; }

    ISshSessionCoordinator Coordinator { get; }

    ITerminalInputController Terminal { get; }
}
