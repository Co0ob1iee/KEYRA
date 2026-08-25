using Microsoft.Extensions.DependencyInjection;
using SshKeyManager.Presentation;
using SshKeyManager.Services.Agent;
using SshKeyManager.Services.Data;
using SshKeyManager.Services.Hardware;
using SshKeyManager.Services.Security;
using SshKeyManager.Services.Ssh;
using SshKeyManager.Services.Update;
using SshKeyManager.ViewModels;

namespace SshKeyManager.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSshKeyManagerServices(this IServiceCollection services)
    {
        services.AddSingleton<VaultPaths>();
        services.AddSingleton<KeyraDb>();
        services.AddSingleton<KeyraRepository>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IShellLayoutService, ShellLayoutService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<Argon2PasswordHasher>();
        services.AddSingleton<AesGcmVaultCrypto>();
        services.AddSingleton<KeyGarageHashService>();
        services.AddSingleton<IVaultSession, VaultSession>();
        services.AddSingleton<IVaultSecurityService, VaultSecurityService>();
        services.AddSingleton<IVaultStore, VaultStore>();
        services.AddSingleton<IOpenSshKeyFactory, OpenSshKeyFactory>();
        services.AddSingleton<IKeyExportService, KeyExportService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IAppLogService, AppLogService>();
        services.AddSingleton<IConnectionAuditService, ConnectionAuditService>();
        services.AddSingleton<ISshConnectionProfileStore, SshConnectionProfileStore>();
        services.AddSingleton<ISshSessionFactory, SshSessionFactory>();
        services.AddSingleton<ISshSessionWindowService, SshSessionWindowService>();
        services.AddSingleton<ISshAgentClient, WindowsOpenSshAgentClient>();
        services.AddSingleton<IKeyraAgentProvider, KeyraAgentProvider>();
        services.AddSingleton<IHardwareKeyService, HardwareKeyService>();
        services.AddSingleton<IAppUpdateService, GitHubAppUpdateService>();
        services.AddSingleton<HardwareKeysViewModel>();
        services.AddTransient<SetupViewModel>();
        services.AddTransient<LoginViewModel>();

        services.AddSingleton<KeysViewModel>();
        services.AddSingleton<GenerateKeyViewModel>();
        services.AddSingleton<ImportKeyViewModel>();
        services.AddSingleton<ConnectionsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        return services;
    }
}
