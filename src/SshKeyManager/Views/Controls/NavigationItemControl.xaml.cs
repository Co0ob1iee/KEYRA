using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SshKeyManager.Views.Controls;

public partial class NavigationItemControl : UserControl
{
    public static readonly DependencyProperty NavigateCommandProperty =
        DependencyProperty.Register(
            nameof(NavigateCommand),
            typeof(ICommand),
            typeof(NavigationItemControl),
            new PropertyMetadata(null));

    public NavigationItemControl()
    {
        InitializeComponent();
    }

    public ICommand? NavigateCommand
    {
        get => (ICommand?)GetValue(NavigateCommandProperty);
        set => SetValue(NavigateCommandProperty, value);
    }
}
