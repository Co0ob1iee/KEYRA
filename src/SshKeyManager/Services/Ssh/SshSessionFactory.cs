namespace SshKeyManager.Services.Ssh;

public sealed class SshSessionFactory : ISshSessionFactory
{
    private readonly IVaultStore _vault;
    private readonly IAppLogService _log;
    private readonly ILocalizationService _localization;
    private readonly IAppSettingsService _settings;

    public SshSessionFactory(
        IVaultStore vault,
        IAppLogService log,
        ILocalizationService localization,
        IAppSettingsService settings)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public ISshSessionScope CreateScope()
    {
        var connection = new SshConnectionService();
        var coordinator = new SshSessionCoordinator(connection, _vault, _log, _localization);
        var terminal = new TerminalInputController(connection, coordinator, _settings, _log, _localization);
        return new SshSessionScope(connection, coordinator, terminal);
    }
}
