using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SshKeyManager.Views.Controls;

public partial class KeyCard : UserControl
{
    public static readonly DependencyProperty SelectKeyCommandProperty =
        DependencyProperty.Register(
            nameof(SelectKeyCommand),
            typeof(ICommand),
            typeof(KeyCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CopyCommandProperty =
        DependencyProperty.Register(
            nameof(CopyCommand),
            typeof(ICommand),
            typeof(KeyCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ExportCommandProperty =
        DependencyProperty.Register(
            nameof(ExportCommand),
            typeof(ICommand),
            typeof(KeyCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(
            nameof(DeleteCommand),
            typeof(ICommand),
            typeof(KeyCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CopyLabelProperty =
        DependencyProperty.Register(
            nameof(CopyLabel),
            typeof(string),
            typeof(KeyCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ExportLabelProperty =
        DependencyProperty.Register(
            nameof(ExportLabel),
            typeof(string),
            typeof(KeyCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DeleteLabelProperty =
        DependencyProperty.Register(
            nameof(DeleteLabel),
            typeof(string),
            typeof(KeyCard),
            new PropertyMetadata(string.Empty));

    public KeyCard()
    {
        InitializeComponent();
    }

    public ICommand? SelectKeyCommand
    {
        get => (ICommand?)GetValue(SelectKeyCommandProperty);
        set => SetValue(SelectKeyCommandProperty, value);
    }

    public ICommand? CopyCommand
    {
        get => (ICommand?)GetValue(CopyCommandProperty);
        set => SetValue(CopyCommandProperty, value);
    }

    public ICommand? ExportCommand
    {
        get => (ICommand?)GetValue(ExportCommandProperty);
        set => SetValue(ExportCommandProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public string CopyLabel
    {
        get => (string)GetValue(CopyLabelProperty);
        set => SetValue(CopyLabelProperty, value);
    }

    public string ExportLabel
    {
        get => (string)GetValue(ExportLabelProperty);
        set => SetValue(ExportLabelProperty, value);
    }

    public string DeleteLabel
    {
        get => (string)GetValue(DeleteLabelProperty);
        set => SetValue(DeleteLabelProperty, value);
    }
}
