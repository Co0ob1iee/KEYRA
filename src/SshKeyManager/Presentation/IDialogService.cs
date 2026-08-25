namespace SshKeyManager.Presentation;

public interface IDialogService
{
    bool Confirm(string message, string title, bool isWarning = false);

    string? PickOpenFile(string title, string filter);

    string? PickFolder(string title);

    void ShowError(string message, string title);

    void ShowInfo(string message, string title);
}
