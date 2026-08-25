using SshKeyManager.ViewModels;

namespace SshKeyManager.Presentation;

/// <summary>
/// Surface exposed by KeysViewModel for the inspector panel without cross-binding Keys.* from the shell.
/// </summary>
public interface IKeyInspectorTarget
{
    KeyListItemViewModel? SelectedKey { get; }

    bool IsPrivateKeyVisible { get; }

    string RevealedPrivateKey { get; }

    string RevealCountdownText { get; }

    void RequestCopyPublicKey();

    void RequestRevealPrivateKey();

    void RequestCopyPrivateKey();

    void RequestExportFolder();

    void RequestExportToSsh();

    void RequestDeleteKey();

    void RequestHidePrivateKey();
}
