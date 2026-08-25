using System.ComponentModel;
using SshKeyManager.Models;
using SshKeyManager.Presentation;
using SshKeyManager.Services;

namespace SshKeyManager.ViewModels;

internal static class ShellModuleBootstrap
{
    public static void Initialize(
        MainViewModel shell,
        INavigationService navigation,
        IShellLayoutService layout)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(layout);

        WireModules(shell, navigation);

        foreach (AppSection section in Enum.GetValues<AppSection>())
        {
            navigation.Register(section, () => shell.ResolveSection(section));
        }

        shell.LogPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LogPanelViewModel.IsExpanded) or nameof(LogPanelViewModel.PanelHeight))
            {
                shell.SyncLogRowHeight();
            }
        };

        navigation.Navigated += (_, _) =>
        {
            shell.SelectedSection = navigation.CurrentSection;
            shell.CurrentViewModel = navigation.CurrentViewModel;
            shell.Inspector.SetVisible(shell.SelectedSection == AppSection.Keys);
            shell.StatusBar.SetSection(shell.SectionStatus(shell.SelectedSection));
            shell.StatusBar.SetStatus(shell.SectionStatus(shell.SelectedSection));
            shell.SyncLogRowHeight();
        };

        navigation.Navigate(AppSection.Keys);
        shell.StatusBar.SetVaultUnlocked();
        shell.StatusBar.SetStatus(shell.SectionStatus(AppSection.Keys));
        shell.StatusBar.SetSection(shell.SectionStatus(AppSection.Keys));
        shell.StatusBar.SetKeyCount(0);
        shell.SyncLogRowHeight();
    }

    private static void WireModules(MainViewModel shell, INavigationService navigation)
    {
        shell.Keys.ConfigureShell(
            setStatus: shell.SetStatus,
            onSelectionChanged: record => shell.Inspector.SetSelectedRecord(record),
            onKeysChanged: count => shell.StatusBar.SetKeyCount(count));

        shell.Connections.ConfigureShell(shell.SetStatus);

        shell.Generate.ConfigureShell(
            shell.SetStatus,
            onKeyCreatedAsync: async () =>
            {
                await shell.Keys.RefreshAsync().ConfigureAwait(true);
                await shell.Connections.LoadKeysAsync().ConfigureAwait(true);
                await shell.Connections.LoadProfilesAsync().ConfigureAwait(true);
                navigation.Navigate(AppSection.Keys);
            });

        shell.Import.ConfigureShell(
            shell.SetStatus,
            onKeyImportedAsync: async () =>
            {
                await shell.Keys.RefreshAsync().ConfigureAwait(true);
                await shell.Connections.LoadKeysAsync().ConfigureAwait(true);
                await shell.Connections.LoadProfilesAsync().ConfigureAwait(true);
                navigation.Navigate(AppSection.Keys);
            });

        shell.Settings.ConfigureShell(shell.SetStatus, onCultureChanged: shell.NotifyCultureChanged);
    }
}
