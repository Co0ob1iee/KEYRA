using System.Collections.ObjectModel;
using System.Windows;
using SshKeyManager.ViewModels;
using SshKeyManager.Views;

namespace SshKeyManager.Services.Ssh;

public sealed class SshSessionWindowService : ISshSessionWindowService
{
    private readonly ISshSessionFactory _factory;
    private readonly IAppLogService _log;
    private readonly ILocalizationService _localization;
    private readonly Dictionary<Guid, SessionEntry> _sessions = new();
    private readonly object _sync = new();

    public SshSessionWindowService(
        ISshSessionFactory factory,
        IAppLogService log,
        ILocalizationService localization)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        ActiveSessions = new ObservableCollection<ActiveSshSessionItemViewModel>();
    }

    public ObservableCollection<ActiveSshSessionItemViewModel> ActiveSessions { get; }

    public int SessionCount
    {
        get
        {
            lock (_sync)
            {
                return _sessions.Count;
            }
        }
    }

    public event EventHandler? SessionsChanged;

    public async Task OpenSessionAsync(SshSessionLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scope = _factory.CreateScope();
        var viewModel = new SshSessionWindowViewModel(scope, request, _log, _localization);
        var item = new ActiveSshSessionItemViewModel(
            viewModel.SessionId,
            request.BuildDisplayTitle(),
            viewModel.HostSummary,
            DisconnectSessionAsync,
            FocusSession);

        Window? window = null;
        try
        {
            window = new SshSessionWindow
            {
                DataContext = viewModel,
                Owner = Application.Current?.MainWindow
            };

            viewModel.RequestClose += (_, _) =>
            {
                try
                {
                    window.Close();
                }
                catch (Exception)
                {
                    // Window may already be closing.
                }
            };

            viewModel.SessionStateChanged += (_, _) => SyncItemFromViewModel(item, viewModel);

            window.Closed += async (_, _) =>
            {
                try
                {
                    await viewModel.PrepareCloseAsync().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    _log.Error($"SSH session close cleanup failed: {ex.Message}");
                }
                finally
                {
                    RemoveSession(viewModel.SessionId, viewModel, dispose: true);
                }
            };

            lock (_sync)
            {
                _sessions[viewModel.SessionId] = new SessionEntry(viewModel, window, item);
            }

            ActiveSessions.Add(item);
            RaiseSessionsChanged();

            window.Show();
            window.Activate();

            await viewModel.ConnectAsync().ConfigureAwait(true);
            SyncItemFromViewModel(item, viewModel);
            RaiseSessionsChanged();
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to open SSH session window: {ex.Message}");
            try
            {
                window?.Close();
            }
            catch (Exception)
            {
                // Ignore.
            }

            RemoveSession(viewModel.SessionId, viewModel, dispose: true);
            throw;
        }
    }

    public void FocusSession(Guid sessionId)
    {
        SessionEntry? entry;
        lock (_sync)
        {
            _sessions.TryGetValue(sessionId, out entry);
        }

        if (entry is null)
        {
            return;
        }

        try
        {
            if (entry.Window.WindowState == WindowState.Minimized)
            {
                entry.Window.WindowState = WindowState.Normal;
            }

            entry.Window.Activate();
            entry.Window.Focus();
            if (entry.Window is SshSessionWindow sessionWindow)
            {
                sessionWindow.FocusTerminalInput();
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to focus SSH session window: {ex.Message}");
        }
    }

    public async Task DisconnectSessionAsync(Guid sessionId)
    {
        SessionEntry? entry;
        lock (_sync)
        {
            _sessions.TryGetValue(sessionId, out entry);
        }

        if (entry is null)
        {
            return;
        }

        try
        {
            await entry.ViewModel.PrepareCloseAsync().ConfigureAwait(true);
            entry.Window.Close();
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to disconnect SSH session: {ex.Message}");
            RemoveSession(sessionId, entry.ViewModel, dispose: true);
        }
    }

    public async Task DisconnectAllAsync()
    {
        List<SessionEntry> entries;
        lock (_sync)
        {
            entries = _sessions.Values.ToList();
        }

        foreach (var entry in entries)
        {
            try
            {
                await entry.ViewModel.PrepareCloseAsync().ConfigureAwait(true);
                entry.Window.Close();
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to close SSH session: {ex.Message}");
                RemoveSession(entry.ViewModel.SessionId, entry.ViewModel, dispose: true);
            }
        }
    }

    private void RemoveSession(Guid sessionId, SshSessionWindowViewModel viewModel, bool dispose)
    {
        SessionEntry? removed;
        lock (_sync)
        {
            _sessions.Remove(sessionId, out removed);
        }

        if (removed is not null)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => RemoveItemFromUi(removed.Item));
            }
            else
            {
                RemoveItemFromUi(removed.Item);
            }
        }

        if (dispose)
        {
            try
            {
                viewModel.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error($"SSH session dispose failed: {ex.Message}");
            }
        }

        RaiseSessionsChanged();
    }

    private void RemoveItemFromUi(ActiveSshSessionItemViewModel item)
    {
        if (ActiveSessions.Contains(item))
        {
            ActiveSessions.Remove(item);
        }
    }

    private static void SyncItemFromViewModel(ActiveSshSessionItemViewModel item, SshSessionWindowViewModel viewModel)
    {
        item.Title = viewModel.WindowTitle;
        item.HostSummary = viewModel.HostSummary;
        item.StatusText = viewModel.ConnectionStatus;
        item.IsConnected = viewModel.IsConnected;
    }

    private void RaiseSessionsChanged() => SessionsChanged?.Invoke(this, EventArgs.Empty);

    private sealed record SessionEntry(
        SshSessionWindowViewModel ViewModel,
        Window Window,
        ActiveSshSessionItemViewModel Item);
}
