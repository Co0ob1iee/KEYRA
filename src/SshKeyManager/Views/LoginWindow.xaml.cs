using System.Windows;
using SshKeyManager.ViewModels;

namespace SshKeyManager.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.RequestClose += (_, accepted) =>
            {
                DialogResult = accepted;
                Close();
            };
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
