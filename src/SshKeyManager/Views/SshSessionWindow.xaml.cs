using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using SshKeyManager.ViewModels;

namespace SshKeyManager.Views;

public partial class SshSessionWindow : Window
{
    private SshSessionWindowViewModel? _viewModel;

    public SshSessionWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Activated += (_, _) => FocusTerminalInput();
    }

    /// <summary>
    /// Focuses the full-width command line under the terminal scrollback.
    /// </summary>
    public void FocusTerminalInput()
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(FocusTerminalInput, DispatcherPriority.Input);
                return;
            }

            SessionCommandInput.FocusInput();
        }
        catch (Exception)
        {
            // Window may be closing.
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FocusTerminalInput();
    }

    private void SessionTerminal_RequestInputFocus(object? sender, EventArgs e)
    {
        FocusTerminalInput();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.RequestClose -= OnRequestClose;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as SshSessionWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.RequestClose += OnRequestClose;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SshSessionWindowViewModel.IsConnected)
            or nameof(SshSessionWindowViewModel.IsBusy)
            or null
            or "")
        {
            if (_viewModel is { IsConnected: true, IsBusy: false })
            {
                FocusTerminalInput();
            }
        }
    }

    private void OnRequestClose(object? sender, EventArgs e)
    {
        try
        {
            Close();
        }
        catch (Exception)
        {
            // Already closing.
        }
    }
}
