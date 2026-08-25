namespace SshKeyManager.Services.Security;

public sealed class VaultPaths
{
    public VaultPaths()
    {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SshKeyManager");
        VaultDirectory = Path.Combine(RootDirectory, "vault");
        DatabasePath = Path.Combine(RootDirectory, "keyra.db");
        MasterKeyFilePath = Path.Combine(RootDirectory, "master.key.enc");
        KeyGarageHashPath = Path.Combine(RootDirectory, "KeyGarageHash");
        IndexPath = Path.Combine(VaultDirectory, "index.json");
        ConnectionsFilePath = Path.Combine(RootDirectory, "connections.json");
        LegacyMigratedMarkerPath = Path.Combine(RootDirectory, ".legacy-migrated");
    }

    public string RootDirectory { get; }

    public string VaultDirectory { get; }

    public string DatabasePath { get; }

    public string MasterKeyFilePath { get; }

    public string KeyGarageHashPath { get; }

    public string IndexPath { get; }

    public string ConnectionsFilePath { get; }

    public string LegacyMigratedMarkerPath { get; }

    public bool HasLegacyVault => File.Exists(MasterKeyFilePath);

    public string GetEncryptedKeyPath(Guid id) =>
        Path.Combine(VaultDirectory, $"{id:N}.key.enc");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(VaultDirectory);
    }
}
