using System.Windows;
using SshKeyManager.ViewModels;

namespace SshKeyManager.Views;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel vm)
        {
            vm.RequestClose += (_, accepted) =>
            {
                DialogResult = accepted;
                Close();
            };
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
