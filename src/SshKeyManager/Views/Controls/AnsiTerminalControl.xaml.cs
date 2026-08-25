using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SshKeyManager.Helpers;

namespace SshKeyManager.Views.Controls;

/// <summary>
/// Dark console view that renders ANSI-colored SSH output incrementally.
/// Cursor addressing / clear-screen are stripped (not emulated) in v1.
/// </summary>
public partial class AnsiTerminalControl : UserControl
{
    public static readonly DependencyProperty AnsiTextProperty =
        DependencyProperty.Register(
            nameof(AnsiText),
            typeof(string),
            typeof(AnsiTerminalControl),
            new PropertyMetadata(string.Empty, OnAnsiTextChanged));

    private readonly AnsiTerminalParser _parser = new();
    private readonly Dictionary<Color, SolidColorBrush> _brushCache = new();
    private Paragraph? _currentParagraph;
    private int _appliedLength;
    private bool _autoScroll = true;

    public AnsiTerminalControl()
    {
        InitializeComponent();
        OutputBox.Document = Document;
        ResetDocument();
        OutputBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
        OutputBox.PreviewMouseLeftButtonUp += OutputBox_PreviewMouseLeftButtonUp;
        OutputBox.PreviewKeyDown += OutputBox_PreviewKeyDown;
    }

    /// <summary>
    /// Raised when the user clicks (without a text selection) or types in the scrollback,
    /// so the host can move focus to the command input line.
    /// </summary>
    public event EventHandler? RequestInputFocus;

    public string AnsiText
    {
        get => (string)GetValue(AnsiTextProperty);
        set => SetValue(AnsiTextProperty, value);
    }

    private static void OnAnsiTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnsiTerminalControl control)
        {
            control.ApplyAnsiText(e.NewValue as string ?? string.Empty);
        }
    }

    private void ApplyAnsiText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            ResetDocument();
            return;
        }

        // SessionOutput is append-only except Clear (empty). Rebuild if the buffer shrank.
        if (text.Length < _appliedLength)
        {
            ResetDocument();
            AppendChunk(text);
            _appliedLength = text.Length;
            ScrollToEndIfNeeded();
            return;
        }

        if (text.Length == _appliedLength)
        {
            return;
        }

        var delta = text[_appliedLength..];
        _appliedLength = text.Length;
        AppendChunk(delta);
        ScrollToEndIfNeeded();
    }

    private void AppendChunk(string chunk)
    {
        var runs = _parser.Parse(chunk);
        foreach (var run in runs)
        {
            AppendStyledText(run.Text, run.Style);
        }
    }

    private void AppendStyledText(string text, AnsiTextStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        EnsureParagraph();

        var start = 0;
        while (start < text.Length)
        {
            var nl = text.IndexOf('\n', start);
            if (nl < 0)
            {
                AppendInline(text[start..], style);
                break;
            }

            if (nl > start)
            {
                AppendInline(text[start..nl], style);
            }

            StartNewParagraph();
            start = nl + 1;
        }
    }

    private void AppendInline(string text, AnsiTextStyle style)
    {
        if (text.Length == 0)
        {
            return;
        }

        EnsureParagraph();
        var inline = new Run(text)
        {
            Foreground = style.Foreground is { } fg
                ? GetBrush(fg)
                : (TryFindResource("TextPrimaryBrush") as Brush) ?? Brushes.WhiteSmoke,
            FontWeight = style.Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = style.Italic ? FontStyles.Italic : FontStyles.Normal
        };

        if (style.Background is { } bg)
        {
            inline.Background = GetBrush(bg);
        }

        if (style.Underline)
        {
            inline.TextDecorations = TextDecorations.Underline;
        }

        _currentParagraph!.Inlines.Add(inline);
    }

    private void EnsureParagraph()
    {
        if (_currentParagraph is not null)
        {
            return;
        }

        _currentParagraph = new Paragraph
        {
            Margin = new Thickness(0),
            Padding = new Thickness(0)
        };
        Document.Blocks.Add(_currentParagraph);
    }

    private void StartNewParagraph()
    {
        _currentParagraph = new Paragraph
        {
            Margin = new Thickness(0),
            Padding = new Thickness(0)
        };
        Document.Blocks.Add(_currentParagraph);
    }

    private void ResetDocument()
    {
        _parser.Reset();
        _appliedLength = 0;
        _currentParagraph = null;
        Document.Blocks.Clear();
        EnsureParagraph();
    }

    private SolidColorBrush GetBrush(Color color)
    {
        if (_brushCache.TryGetValue(color, out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        _brushCache[color] = brush;
        return brush;
    }

    private void ScrollToEndIfNeeded()
    {
        if (!_autoScroll)
        {
            return;
        }

        OutputBox.CaretPosition = OutputBox.Document.ContentEnd;
        OutputBox.ScrollToEnd();
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange != 0)
        {
            return;
        }

        // Keep auto-scroll when the viewport is near the bottom.
        var viewer = FindScrollViewer(OutputBox);
        if (viewer is null)
        {
            return;
        }

        _autoScroll = viewer.VerticalOffset + viewer.ViewportHeight >= viewer.ExtentHeight - 4;
    }

    private void OutputBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            // Keep focus on output when the user selected text (for copy); otherwise focus input.
            if (string.IsNullOrEmpty(OutputBox.Selection?.Text))
            {
                RequestInputFocus?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception)
        {
            // Ignore focus routing failures.
        }
    }

    private void OutputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Typing in scrollback should land on the command line (terminal UX).
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.Tab or Key.Escape or Key.CapsLock or Key.NumLock or Key.Scroll)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            && e.Key is Key.C or Key.A)
        {
            return;
        }

        RequestInputFocus?.Invoke(this, EventArgs.Empty);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv)
        {
            return sv;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var found = FindScrollViewer(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
