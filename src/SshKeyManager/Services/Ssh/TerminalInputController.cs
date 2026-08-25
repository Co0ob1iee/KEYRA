using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SshKeyManager.Helpers;

namespace SshKeyManager.Services.Ssh;

public sealed partial class TerminalInputController : ObservableObject, ITerminalInputController
{
    private readonly ISshConnectionService _ssh;
    private readonly ISshSessionCoordinator _session;
    private readonly IAppSettingsService _settings;
    private readonly IAppLogService _log;
    private readonly ILocalizationService _localization;
    private Action<string> _setStatus = _ => { };
    private readonly List<string> _commandHistory = new();
    private IReadOnlyList<string> _currentMatches = Array.Empty<string>();
    private int _historyBrowseIndex = -1;
    private string _historyBrowseDraft = string.Empty;
    private int _tabCycleIndex = -1;
    private bool _suppressSuggestionRefresh;
    private bool _isRemoteCompletionPopup;
    private string _completionPrefix = string.Empty;
    private string _completionPartial = string.Empty;
    private int _lastCompletionCursorPosition;

    public TerminalInputController(
        ISshConnectionService ssh,
        ISshSessionCoordinator session,
        IAppSettingsService settings,
        IAppLogService log,
        ILocalizationService localization)
    {
        _ssh = ssh ?? throw new ArgumentNullException(nameof(ssh));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));

        LoadCommandHistory();
    }

    public ObservableCollection<string> FilteredSuggestions { get; } = new();

    [ObservableProperty]
    private string _commandText = string.Empty;

    [ObservableProperty]
    private string _ghostSuggestion = string.Empty;

    [ObservableProperty]
    private bool _isSuggestionPopupOpen;

    [ObservableProperty]
    private int _selectedSuggestionIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTerminalInputEnabled))]
    private bool _isCompleting;

    public bool IsTerminalInputEnabled => !IsCompleting;

    public void ConfigureShell(Action<string> setStatus)
    {
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
    }

    public void HandleTabKey(int cursorPosition)
    {
        if (!string.IsNullOrEmpty(GhostSuggestion))
        {
            AcceptGhostSuggestion();
            return;
        }

        if (IsSuggestionPopupOpen)
        {
            CycleSuggestionPopup();
            return;
        }

        if (_currentMatches.Count > 0)
        {
            OpenSuggestionPopup(startIndex: 0);
            return;
        }

        if (_session.IsConnected && !IsCompleting && !_session.IsBusy)
        {
            _ = RequestRemoteCompletionAsync(cursorPosition);
        }
    }

    public void CycleSuggestionForward(int cursorPosition)
    {
        if (!IsSuggestionPopupOpen)
        {
            return;
        }

        _lastCompletionCursorPosition = cursorPosition;
        CycleSuggestionPopup();
    }

    public void AcceptGhostSuggestion()
    {
        if (string.IsNullOrEmpty(GhostSuggestion))
        {
            return;
        }

        CommandText += GhostSuggestion;
        _tabCycleIndex = -1;
        RefreshSuggestions();
    }

    public void NavigateHistoryPrevious()
    {
        if (IsSuggestionPopupOpen)
        {
            MovePopupSelection(-1);
            return;
        }

        if (_commandHistory.Count == 0)
        {
            return;
        }

        if (_historyBrowseIndex < 0)
        {
            _historyBrowseDraft = CommandText;
            _historyBrowseIndex = _commandHistory.Count;
        }

        if (_historyBrowseIndex > 0)
        {
            _historyBrowseIndex--;
            SetCommandTextFromHistory(_commandHistory[_historyBrowseIndex]);
        }
    }

    public void NavigateHistoryNext()
    {
        if (IsSuggestionPopupOpen)
        {
            MovePopupSelection(1);
            return;
        }

        if (_historyBrowseIndex < 0)
        {
            return;
        }

        _historyBrowseIndex++;
        if (_historyBrowseIndex >= _commandHistory.Count)
        {
            _historyBrowseIndex = -1;
            SetCommandTextFromHistory(_historyBrowseDraft);
        }
        else
        {
            SetCommandTextFromHistory(_commandHistory[_historyBrowseIndex]);
        }
    }

    public void AcceptSelectedSuggestion()
    {
        if (SelectedSuggestionIndex >= 0 && SelectedSuggestionIndex < FilteredSuggestions.Count)
        {
            SelectSuggestion(FilteredSuggestions[SelectedSuggestionIndex]);
            return;
        }

        AcceptGhostSuggestion();
    }

    public void SelectSuggestion(string suggestion)
    {
        if (_isRemoteCompletionPopup)
        {
            ApplyRemoteCompletionAtCursor(suggestion, CommandText, _lastCompletionCursorPosition);
            CloseSuggestionPopup();
            return;
        }

        SetCommandTextFromHistory(suggestion);
        CloseSuggestionPopup();
    }

    public void CloseSuggestionPopup()
    {
        IsSuggestionPopupOpen = false;
        SelectedSuggestionIndex = -1;
        _tabCycleIndex = -1;
        _isRemoteCompletionPopup = false;
        _completionPrefix = string.Empty;
        _completionPartial = string.Empty;
    }

    public void ClearInputLine()
    {
        CommandText = string.Empty;
        _historyBrowseIndex = -1;
        _historyBrowseDraft = string.Empty;
        CloseSuggestionPopup();
        RefreshSuggestions();
    }

    public void AddCommandToHistory(string command)
    {
        var maxCount = Math.Max(1, _settings.Settings.SshCommandHistoryMaxCount);
        var updated = TerminalSuggestionEngine.AppendHistory(_commandHistory, command, maxCount);
        _commandHistory.Clear();
        _commandHistory.AddRange(updated);
        _settings.Settings.SshCommandHistory = new List<string>(_commandHistory);
        _ = PersistCommandHistoryAsync();
        RefreshSuggestions();
    }

    public void ResetInputAfterSend()
    {
        _suppressSuggestionRefresh = true;
        CommandText = string.Empty;
        _suppressSuggestionRefresh = false;
        _historyBrowseIndex = -1;
        _historyBrowseDraft = string.Empty;
        CloseSuggestionPopup();
        RefreshSuggestions();
    }

    partial void OnCommandTextChanged(string value)
    {
        if (!_suppressSuggestionRefresh)
        {
            _historyBrowseIndex = -1;
            _historyBrowseDraft = string.Empty;
            _tabCycleIndex = -1;
            if (_isRemoteCompletionPopup)
            {
                CloseSuggestionPopup();
            }

            RefreshSuggestions();
        }
    }

    private async Task PersistCommandHistoryAsync()
    {
        try
        {
            await _settings.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to persist SSH command history: {ex.Message}");
        }
    }

    private void LoadCommandHistory()
    {
        _commandHistory.Clear();
        var saved = _settings.Settings.SshCommandHistory;
        if (saved is { Count: > 0 })
        {
            _commandHistory.AddRange(saved);
        }

        RefreshSuggestions();
    }

    private void RefreshSuggestions()
    {
        GhostSuggestion = TerminalSuggestionEngine.FindGhostSuffix(CommandText, _commandHistory) ?? string.Empty;

        if (!_isRemoteCompletionPopup)
        {
            _currentMatches = TerminalSuggestionEngine.FindMatches(CommandText, _commandHistory);

            FilteredSuggestions.Clear();
            foreach (var match in _currentMatches)
            {
                FilteredSuggestions.Add(match);
            }
        }

        if (IsSuggestionPopupOpen && FilteredSuggestions.Count == 0)
        {
            CloseSuggestionPopup();
        }
        else if (IsSuggestionPopupOpen && SelectedSuggestionIndex >= FilteredSuggestions.Count)
        {
            SelectedSuggestionIndex = FilteredSuggestions.Count - 1;
        }
    }

    private void SetCommandTextFromHistory(string text)
    {
        _suppressSuggestionRefresh = true;
        CommandText = text;
        _suppressSuggestionRefresh = false;
        RefreshSuggestions();
    }

    private void ApplyMatchAtIndex(int index)
    {
        if (index < 0 || index >= _currentMatches.Count)
        {
            return;
        }

        if (_isRemoteCompletionPopup)
        {
            ApplyRemoteCompletionAtCursor(_currentMatches[index], CommandText, _lastCompletionCursorPosition);
            return;
        }

        SetCommandTextFromHistory(_currentMatches[index]);
    }

    private void OpenSuggestionPopup(int startIndex)
    {
        IsSuggestionPopupOpen = true;
        _tabCycleIndex = startIndex;
        SelectedSuggestionIndex = startIndex;
        ApplyMatchAtIndex(_tabCycleIndex);
    }

    private void CycleSuggestionPopup()
    {
        if (_currentMatches.Count == 0)
        {
            CloseSuggestionPopup();
            return;
        }

        _tabCycleIndex = (_tabCycleIndex + 1) % _currentMatches.Count;
        SelectedSuggestionIndex = _tabCycleIndex;
        ApplyMatchAtIndex(_tabCycleIndex);
    }

    private async Task RequestRemoteCompletionAsync(int cursorPosition)
    {
        var fullLine = CommandText;
        var lineBeforeCursor = cursorPosition <= fullLine.Length
            ? fullLine[..cursorPosition]
            : fullLine;

        IsCompleting = true;
        _session.ConnectionStatus = L("Connections_Completing");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_session.SessionToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var matches = await _ssh.RequestTabCompletionAsync(
                lineBeforeCursor,
                cursorPosition,
                cts.Token).ConfigureAwait(true);

            (_completionPrefix, _completionPartial) =
                TerminalSuggestionEngine.GetPartialWord(lineBeforeCursor);
            _lastCompletionCursorPosition = cursorPosition;

            if (matches.Count == 0)
            {
                return;
            }

            if (matches.Count == 1)
            {
                ApplyRemoteCompletionAtCursor(matches[0], fullLine, cursorPosition);
                RefreshSuggestions();
                return;
            }

            _isRemoteCompletionPopup = true;
            _currentMatches = matches;
            FilteredSuggestions.Clear();
            foreach (var match in _currentMatches)
            {
                FilteredSuggestions.Add(match);
            }

            OpenSuggestionPopup(startIndex: 0);
        }
        catch (OperationCanceledException)
        {
            _log.Error("Remote tab completion was cancelled.");
            _setStatus(L("Connections_CompletionFailed", L("Connections_CompletionCancelled")));
        }
        catch (Exception ex)
        {
            _log.Error($"Remote tab completion failed: {ex.Message}");
            _setStatus(L("Connections_CompletionFailed", ex.Message));
        }
        finally
        {
            IsCompleting = false;
            _session.RefreshConnectionStatusLabel();
        }
    }

    private void ApplyRemoteCompletionAtCursor(string match, string fullLine, int cursorPosition)
    {
        var lineBeforeCursor = cursorPosition <= fullLine.Length
            ? fullLine[..cursorPosition]
            : fullLine;
        var textAfterCursor = cursorPosition < fullLine.Length
            ? fullLine[cursorPosition..]
            : string.Empty;
        var (prefix, _) = TerminalSuggestionEngine.GetPartialWord(lineBeforeCursor);
        SetCommandTextWithoutHistoryReset(prefix + match + textAfterCursor);
    }

    private void SetCommandTextWithoutHistoryReset(string text)
    {
        _suppressSuggestionRefresh = true;
        CommandText = text;
        _suppressSuggestionRefresh = false;
        RefreshSuggestions();
    }

    private void MovePopupSelection(int delta)
    {
        if (FilteredSuggestions.Count == 0)
        {
            return;
        }

        var next = SelectedSuggestionIndex + delta;
        if (next < 0)
        {
            next = FilteredSuggestions.Count - 1;
        }
        else if (next >= FilteredSuggestions.Count)
        {
            next = 0;
        }

        SelectedSuggestionIndex = next;
        _tabCycleIndex = next;
        ApplyMatchAtIndex(next);
    }

    private string L(string key) => _localization.GetString(key);

    private string L(string key, params object[] args) => _localization.GetString(key, args);
}
