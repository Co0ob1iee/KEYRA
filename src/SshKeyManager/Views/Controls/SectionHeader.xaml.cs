using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SshKeyManager.Views.Controls;

public partial class SectionHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(SectionHeader),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionContentProperty =
        DependencyProperty.Register(
            nameof(ActionContent),
            typeof(object),
            typeof(SectionHeader),
            new PropertyMetadata(null, OnActionSlotChanged));

    public static readonly DependencyProperty ActionLabelProperty =
        DependencyProperty.Register(
            nameof(ActionLabel),
            typeof(string),
            typeof(SectionHeader),
            new PropertyMetadata(null, OnActionSlotChanged));

    public static readonly DependencyProperty ActionCommandProperty =
        DependencyProperty.Register(
            nameof(ActionCommand),
            typeof(ICommand),
            typeof(SectionHeader),
            new PropertyMetadata(null, OnActionSlotChanged));

    public SectionHeader()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateActionSlotVisibility();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public string? ActionLabel
    {
        get => (string?)GetValue(ActionLabelProperty);
        set => SetValue(ActionLabelProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    private static void OnActionSlotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SectionHeader header)
        {
            header.UpdateActionSlotVisibility();
        }
    }

    private void UpdateActionSlotVisibility()
    {
        if (ActionSlot is null || ActionButton is null)
        {
            return;
        }

        var hasContent = ActionContent is not null;
        ActionSlot.Visibility = hasContent ? Visibility.Visible : Visibility.Collapsed;

        var hasLabelAction = !hasContent && !string.IsNullOrWhiteSpace(ActionLabel);
        ActionButton.Visibility = hasLabelAction ? Visibility.Visible : Visibility.Collapsed;
    }
}
