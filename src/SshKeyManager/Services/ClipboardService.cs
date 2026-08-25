using System.Windows;

namespace SshKeyManager.Services;

public sealed class ClipboardService : IClipboardService
{
    public void SetText(string text, bool isPrivateKey)
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Clipboard text is empty.", nameof(text));
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to copy to clipboard.", ex);
        }
    }
}
