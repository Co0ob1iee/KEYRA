using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using SshKeyManager.Helpers;
using SshKeyManager.Models;

namespace SshKeyManager.Services;

public sealed class AppLogService : IAppLogService
{
    private const int MaxEntries = 500;

    public ObservableCollection<AppLogEntry> Entries { get; } = new();

    public void Info(string message) => Add("INFO", message);

    public void Warning(string message) => Add("WARN", message);

    public void Error(string message) => Add("ERROR", message);

    public void Clear()
    {
        RunOnUi(() => Entries.Clear());
    }

    private void Add(string level, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // Never log private key material markers.
        if (message.Contains("BEGIN OPENSSH PRIVATE KEY", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("BEGIN RSA PRIVATE KEY", StringComparison.OrdinalIgnoreCase))
        {
            message = "[redacted private key content]";
        }

        // Defense in depth: never persist raw CSI/OSC sequences in the log panel.
        if (message.IndexOf('\u001b') >= 0 || message.IndexOf('\a') >= 0)
        {
            message = AnsiTerminalParser.Strip(message);
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }
        }

        var entry = new AppLogEntry(DateTime.UtcNow, level, message.Trim());
        RunOnUi(() =>
        {
            Entries.Insert(0, entry);
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
        });
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action, DispatcherPriority.DataBind);
    }
}
