using System.Windows;
using System.Windows.Controls;

namespace SshKeyManager.Views.Controls;

public partial class TitleBarControl : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(TitleBarControl),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowMinimizeButtonProperty =
        DependencyProperty.Register(
            nameof(ShowMinimizeButton),
            typeof(bool),
            typeof(TitleBarControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowMaximizeButtonProperty =
        DependencyProperty.Register(
            nameof(ShowMaximizeButton),
            typeof(bool),
            typeof(TitleBarControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowCloseButtonProperty =
        DependencyProperty.Register(
            nameof(ShowCloseButton),
            typeof(bool),
            typeof(TitleBarControl),
            new PropertyMetadata(true));

    private Window? _hostWindow;

    public TitleBarControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowMinimizeButton
    {
        get => (bool)GetValue(ShowMinimizeButtonProperty);
        set => SetValue(ShowMinimizeButtonProperty, value);
    }

    public bool ShowMaximizeButton
    {
        get => (bool)GetValue(ShowMaximizeButtonProperty);
        set => SetValue(ShowMaximizeButtonProperty, value);
    }

    public bool ShowCloseButton
    {
        get => (bool)GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hostWindow = Window.GetWindow(this);
        if (_hostWindow is null)
        {
            return;
        }

        _hostWindow.StateChanged += HostWindow_StateChanged;
        UpdateMaximizeGlyph();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_hostWindow is not null)
        {
            _hostWindow.StateChanged -= HostWindow_StateChanged;
            _hostWindow = null;
        }
    }

    private void HostWindow_StateChanged(object? sender, EventArgs e) => UpdateMaximizeGlyph();

    private void UpdateMaximizeGlyph()
    {
        if (_hostWindow is null)
        {
            return;
        }

        var maximized = _hostWindow.WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? "❐" : "☐";
        MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }

        window.WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        window?.Close();
    }
}
