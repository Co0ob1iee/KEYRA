using System.Collections.ObjectModel;
using SshKeyManager.Models;

namespace SshKeyManager.Services;

public interface IAppLogService
{
    ObservableCollection<AppLogEntry> Entries { get; }

    void Info(string message);

    void Warning(string message);

    void Error(string message);

    void Clear();
}
