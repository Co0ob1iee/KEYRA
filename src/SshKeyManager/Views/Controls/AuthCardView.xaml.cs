using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace SshKeyManager.Views.Controls;

[ContentProperty(nameof(FormContent))]
public partial class AuthCardView : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(AuthCardView),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(
            nameof(Subtitle),
            typeof(string),
            typeof(AuthCardView),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FormContentProperty =
        DependencyProperty.Register(
            nameof(FormContent),
            typeof(object),
            typeof(AuthCardView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(
            nameof(ErrorMessage),
            typeof(string),
            typeof(AuthCardView),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.Register(
            nameof(IsBusy),
            typeof(bool),
            typeof(AuthCardView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty BusyMessageProperty =
        DependencyProperty.Register(
            nameof(BusyMessage),
            typeof(string),
            typeof(AuthCardView),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PrimaryButtonContentProperty =
        DependencyProperty.Register(
            nameof(PrimaryButtonContent),
            typeof(object),
            typeof(AuthCardView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty PrimaryButtonCommandProperty =
        DependencyProperty.Register(
            nameof(PrimaryButtonCommand),
            typeof(ICommand),
            typeof(AuthCardView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SecondaryButtonContentProperty =
        DependencyProperty.Register(
            nameof(SecondaryButtonContent),
            typeof(object),
            typeof(AuthCardView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SecondaryButtonCommandProperty =
        DependencyProperty.Register(
            nameof(SecondaryButtonCommand),
            typeof(ICommand),
            typeof(AuthCardView),
            new PropertyMetadata(null));

    public static readonly RoutedEvent SecondaryClickEvent =
        EventManager.RegisterRoutedEvent(
            nameof(SecondaryClick),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(AuthCardView));

    public AuthCardView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? FormContent
    {
        get => GetValue(FormContentProperty);
        set => SetValue(FormContentProperty, value);
    }

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public string BusyMessage
    {
        get => (string)GetValue(BusyMessageProperty);
        set => SetValue(BusyMessageProperty, value);
    }

    public object? PrimaryButtonContent
    {
        get => GetValue(PrimaryButtonContentProperty);
        set => SetValue(PrimaryButtonContentProperty, value);
    }

    public ICommand? PrimaryButtonCommand
    {
        get => (ICommand?)GetValue(PrimaryButtonCommandProperty);
        set => SetValue(PrimaryButtonCommandProperty, value);
    }

    public object? SecondaryButtonContent
    {
        get => GetValue(SecondaryButtonContentProperty);
        set => SetValue(SecondaryButtonContentProperty, value);
    }

    public ICommand? SecondaryButtonCommand
    {
        get => (ICommand?)GetValue(SecondaryButtonCommandProperty);
        set => SetValue(SecondaryButtonCommandProperty, value);
    }

    public event RoutedEventHandler SecondaryClick
    {
        add => AddHandler(SecondaryClickEvent, value);
        remove => RemoveHandler(SecondaryClickEvent, value);
    }

    private void SecondaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(SecondaryClickEvent, this));
    }
}
