namespace SshKeyManager.Services.Ssh;

public sealed class SshSessionFactory : ISshSessionFactory
{
    private readonly IVaultStore _vault;
    private readonly IAppLogService _log;
    private readonly ILocalizationService _localization;
    private readonly IAppSettingsService _settings;
    private readonly IConnectionAuditService _audit;

    public SshSessionFactory(
        IVaultStore vault,
        IAppLogService log,
        ILocalizationService localization,
        IAppSettingsService settings,
        IConnectionAuditService audit)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public ISshSessionScope CreateScope()
    {
        var connection = new SshConnectionService();
        var coordinator = new SshSessionCoordinator(connection, _vault, _log, _localization, _audit);
        var terminal = new TerminalInputController(connection, coordinator, _settings, _log, _localization);
        return new SshSessionScope(connection, coordinator, terminal);
    }
}
