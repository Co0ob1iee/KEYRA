using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SshKeyManager.Services.Ssh;
using SshKeyManager.ViewModels;

namespace SshKeyManager.Views.Controls;

public partial class TerminalInputControl
{
    private INotifyPropertyChanged? _subscribedNotifier;
    private ITerminalInputController? _terminal;
    private ITerminalSessionHost? _host;

    public TerminalInputControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
        Loaded += (_, _) => AttachViewModel();
        Unloaded += (_, _) => DetachViewModel();
    }

    /// <summary>
    /// Moves keyboard focus to the command line (session open / click-in-output UX).
    /// </summary>
    public void FocusInput()
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(FocusInput, DispatcherPriority.Input);
                return;
            }

            InputBox.Focus();
            Keyboard.Focus(InputBox);
            var length = InputBox.Text?.Length ?? 0;
            InputBox.CaretIndex = length;
            InputBox.SelectionStart = length;
            InputBox.SelectionLength = 0;
        }
        catch (Exception)
        {
            // Control may be unloading.
        }
    }

    private ITerminalSessionHost? Host =>
        _host ?? DataContext as ITerminalSessionHost;

    private ITerminalInputController? Terminal =>
        _terminal ?? Host?.Terminal;

    private void AttachViewModel()
    {
        DetachViewModel();
        _host = DataContext as ITerminalSessionHost;
        _terminal = _host?.Terminal;
        _subscribedNotifier = _host as INotifyPropertyChanged ?? _terminal;
        if (_subscribedNotifier is not null)
        {
            _subscribedNotifier.PropertyChanged += ViewModel_PropertyChanged;
        }

        RefreshGhostOverlay();
    }

    private void DetachViewModel()
    {
        if (_subscribedNotifier is not null)
        {
            _subscribedNotifier.PropertyChanged -= ViewModel_PropertyChanged;
            _subscribedNotifier = null;
        }

        _terminal = null;
        _host = null;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ITerminalInputController.GhostSuggestion)
            or nameof(ITerminalInputController.CommandText)
            or nameof(SshSessionWindowViewModel.GhostSuggestion)
            or nameof(SshSessionWindowViewModel.CommandText)
            or null
            or "")
        {
            RefreshGhostOverlay();
        }
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshGhostOverlay();
    }

    private void RefreshGhostOverlay()
    {
        var typed = InputBox.Text ?? string.Empty;
        var ghost = Terminal?.GhostSuggestion ?? string.Empty;

        if (string.IsNullOrEmpty(ghost))
        {
            GhostOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        GhostPrefixRun.Text = typed;
        GhostSuffixRun.Text = ghost;
        GhostOverlay.Visibility = Visibility.Visible;
    }

    private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var terminal = Terminal;
        var host = Host;
        if (terminal is null || host is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Tab:
                e.Handled = true;
                terminal.HandleTabKey(InputBox.CaretIndex);
                RefreshGhostOverlay();
                break;

            case Key.Up:
                if (CanNavigateHistory(e.Key))
                {
                    e.Handled = true;
                    terminal.NavigateHistoryPrevious();
                    RefreshGhostOverlay();
                }

                break;

            case Key.Down:
                if (CanNavigateHistory(e.Key))
                {
                    e.Handled = true;
                    terminal.NavigateHistoryNext();
                    RefreshGhostOverlay();
                }

                break;

            case Key.Enter:
                if (terminal.IsSuggestionPopupOpen)
                {
                    e.Handled = true;
                    terminal.AcceptSelectedSuggestion();
                    RefreshGhostOverlay();
                }
                else if (host.IsConnected && host.SendCommandCommand.CanExecute(null))
                {
                    e.Handled = true;
                    host.SendCommandCommand.Execute(null);
                    RefreshGhostOverlay();
                }

                break;

            case Key.Escape:
                if (terminal.IsSuggestionPopupOpen)
                {
                    e.Handled = true;
                    terminal.CloseSuggestionPopup();
                }

                break;

            case Key.C when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                e.Handled = true;
                terminal.ClearInputLine();
                RefreshGhostOverlay();
                break;

            case Key.Right when Keyboard.Modifiers == ModifierKeys.None:
                if (terminal.IsSuggestionPopupOpen)
                {
                    e.Handled = true;
                    terminal.CycleSuggestionForward(InputBox.CaretIndex);
                    RefreshGhostOverlay();
                }

                break;
        }
    }

    private bool CanNavigateHistory(Key key)
    {
        var terminal = Terminal;
        if (terminal is null)
        {
            return false;
        }

        if (terminal.IsSuggestionPopupOpen)
        {
            return true;
        }

        var caretAtEnd = InputBox.CaretIndex >= (InputBox.Text?.Length ?? 0);
        var caretAtStart = InputBox.CaretIndex <= 0;

        return key switch
        {
            Key.Up => caretAtEnd || caretAtStart,
            Key.Down => caretAtEnd || caretAtStart,
            _ => false
        };
    }

    private void SuggestionList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var terminal = Terminal;
        if (terminal is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                terminal.AcceptSelectedSuggestion();
                InputBox.Focus();
                RefreshGhostOverlay();
                break;

            case Key.Escape:
                e.Handled = true;
                terminal.CloseSuggestionPopup();
                InputBox.Focus();
                break;

            case Key.Tab:
                e.Handled = true;
                terminal.HandleTabKey(InputBox.CaretIndex);
                RefreshGhostOverlay();
                break;
        }
    }

    private void SuggestionList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SuggestionList.SelectedItem is string suggestion)
        {
            Terminal?.SelectSuggestion(suggestion);
            InputBox.Focus();
            RefreshGhostOverlay();
        }
    }

    private void SuggestionPopup_Opened(object? sender, EventArgs e)
    {
        if (Terminal?.SelectedSuggestionIndex is >= 0)
        {
            SuggestionList.ScrollIntoView(SuggestionList.SelectedItem);
        }
    }
}
