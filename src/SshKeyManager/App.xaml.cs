using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SshKeyManager.Services;
using SshKeyManager.Services.Security;
using SshKeyManager.Services.Ssh;
using SshKeyManager.ViewModels;
using SshKeyManager.Views;

namespace SshKeyManager;

public partial class App : Application
{
    private IHost? _host;
    private MainWindow? _mainWindow;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) => services.AddSshKeyManagerServices())
                .Build();

            Services = _host.Services;
            await _host.StartAsync().ConfigureAwait(true);

            await ApplyStoredCultureAsync().ConfigureAwait(true);

            if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true))
            {
                Shutdown(0);
                return;
            }

            await ShowMainShellAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            var detail = FormatExceptionDetail(ex);
            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SshKeyManager",
                    "startup-error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.WriteAllText(logPath, detail);
            }
            catch (Exception)
            {
                // Best-effort diagnostics only.
            }

            var loc = Services.GetService<ILocalizationService>();
            var message = loc is null
                ? $"Failed to start KEYRA.{Environment.NewLine}{detail}"
                : loc.GetString("App_StartupFailed", detail);
            message = NormalizeDisplayNewlines(message);

            MessageBox.Show(
                message,
                loc?.GetString("App_Title") ?? "KEYRA",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    private static async Task ApplyStoredCultureAsync()
    {
        var settingsService = Services.GetRequiredService<IAppSettingsService>();
        var localization = Services.GetRequiredService<ILocalizationService>();
        await settingsService.LoadAsync().ConfigureAwait(true);
        localization.SetCulture(settingsService.Settings.Language);
    }

    private async Task<bool> EnsureVaultUnlockedAsync()
    {
        var security = Services.GetRequiredService<IVaultSecurityService>();

        while (true)
        {
            if (!security.IsSetupComplete)
            {
                var setupVm = Services.GetRequiredService<SetupViewModel>();
                var setupWindow = new SetupWindow { DataContext = setupVm, Owner = _mainWindow };
                if (setupWindow.ShowDialog() != true)
                {
                    return false;
                }

                return true;
            }

            var loginVm = Services.GetRequiredService<LoginViewModel>();
            var loginWindow = new LoginWindow { DataContext = loginVm, Owner = _mainWindow };
            if (loginWindow.ShowDialog() == true)
            {
                return true;
            }

            return false;
        }
    }

    private async Task ShowMainShellAsync()
    {
        var mainViewModel = Services.GetRequiredService<MainViewModel>();
        mainViewModel.RequestRelogin += async (_, _) =>
        {
            _mainWindow?.Hide();
            var security = Services.GetRequiredService<IVaultSecurityService>();
            security.Lock();

            if (await EnsureVaultUnlockedAsync().ConfigureAwait(true))
            {
                await mainViewModel.InitializeCommand.ExecuteAsync(null).ConfigureAwait(true);
                _mainWindow?.Show();
            }
            else
            {
                Shutdown(0);
            }
        };

        await mainViewModel.InitializeCommand.ExecuteAsync(null).ConfigureAwait(true);

        _mainWindow = new MainWindow
        {
            DataContext = mainViewModel
        };

        MainWindow = _mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        _mainWindow.Show();
    }

    private static string NormalizeDisplayNewlines(string message) =>
        message
            .Replace("\\r\\n", Environment.NewLine, StringComparison.Ordinal)
            .Replace("\\n", Environment.NewLine, StringComparison.Ordinal);

    private static string FormatExceptionDetail(Exception ex)
    {
        var sb = new StringBuilder();
        var current = ex;
        var depth = 0;
        while (current is not null)
        {
            if (depth > 0)
            {
                sb.AppendLine();
                sb.Append("Inner: ");
            }

            sb.Append(current.GetType().FullName);
            sb.Append(": ");
            sb.Append(current.Message);

            if (current is System.Windows.Markup.XamlParseException xamlEx)
            {
                sb.Append(" [Line ");
                sb.Append(xamlEx.LineNumber);
                sb.Append(", Pos ");
                sb.Append(xamlEx.LinePosition);
                sb.Append(']');
                if (!string.IsNullOrWhiteSpace(xamlEx.BaseUri?.ToString()))
                {
                    sb.Append(" URI=");
                    sb.Append(xamlEx.BaseUri);
                }
            }

            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                sb.AppendLine();
                sb.Append(current.StackTrace);
            }

            current = current.InnerException;
            depth++;
        }

        return sb.ToString();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            Services.GetService<IVaultSecurityService>()?.Lock();
            var sessions = Services.GetService<ISshSessionWindowService>();
            if (sessions is not null)
            {
                sessions.DisconnectAllAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception)
        {
            // Best-effort shutdown.
        }

        if (_host is not null)
        {
            try
            {
                await _host.StopAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Best-effort shutdown.
            }

            _host.Dispose();
            _host = null;
        }

        base.OnExit(e);
    }
}
