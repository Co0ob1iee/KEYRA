namespace SshKeyManager.Services.Ssh;

public sealed class SshSessionScope : ISshSessionScope
{
    private bool _disposed;

    public SshSessionScope(
        ISshConnectionService connection,
        ISshSessionCoordinator coordinator,
        ITerminalInputController terminal)
    {
        SessionId = Guid.NewGuid();
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        Terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
    }

    public Guid SessionId { get; }

    public ISshConnectionService Connection { get; }

    public ISshSessionCoordinator Coordinator { get; }

    public ITerminalInputController Terminal { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (Coordinator is IDisposable disposableCoordinator)
            {
                disposableCoordinator.Dispose();
            }
        }
        catch (Exception)
        {
            // Best-effort dispose.
        }

        try
        {
            Connection.Dispose();
        }
        catch (Exception)
        {
            // Best-effort dispose.
        }
    }
}
