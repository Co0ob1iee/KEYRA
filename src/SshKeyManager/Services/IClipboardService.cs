namespace SshKeyManager.Services;

public interface IClipboardService
{
    void SetText(string text, bool isPrivateKey);
}
