using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SshKeyManager.Services.Ssh;

public interface ITerminalInputController : INotifyPropertyChanged
{
    string CommandText { get; set; }

    string GhostSuggestion { get; }

    bool IsSuggestionPopupOpen { get; set; }

    int SelectedSuggestionIndex { get; set; }

    ObservableCollection<string> FilteredSuggestions { get; }

    bool IsCompleting { get; }

    bool IsTerminalInputEnabled { get; }

    void ConfigureShell(Action<string> setStatus);

    void HandleTabKey(int cursorPosition);

    void CycleSuggestionForward(int cursorPosition);

    void AcceptGhostSuggestion();

    void NavigateHistoryPrevious();

    void NavigateHistoryNext();

    void AcceptSelectedSuggestion();

    void SelectSuggestion(string suggestion);

    void CloseSuggestionPopup();

    void ClearInputLine();

    void AddCommandToHistory(string command);

    void ResetInputAfterSend();
}
