using System.Windows;
using Microsoft.Win32;
using SshKeyManager.Presentation;

namespace SshKeyManager.Services;

public sealed class DialogService : IDialogService
{
    public bool Confirm(string message, string title, bool isWarning = false)
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            isWarning ? MessageBoxImage.Warning : MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public string? PickOpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };

        return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName)
            ? dialog.FolderName
            : null;
    }

    public void ShowError(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowInfo(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
