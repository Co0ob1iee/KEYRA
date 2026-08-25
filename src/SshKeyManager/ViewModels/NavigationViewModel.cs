using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Models;
using SshKeyManager.Presentation;
using SshKeyManager.Services;

namespace SshKeyManager.ViewModels;

public partial class NavigationViewModel : LocalizedViewModelBase
{
    private readonly INavigationService _navigation;

    public NavigationViewModel(ILocalizationService localization, INavigationService navigation)
        : base(localization)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        NavigationItems = new ObservableCollection<NavigationItem>();
        RefreshNavigationItems();
        _navigation.Navigated += (_, _) =>
        {
            SelectedSection = _navigation.CurrentSection;
            RefreshNavigationItems();
        };
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    [ObservableProperty]
    private AppSection _selectedSection = AppSection.Keys;

    public string LockVaultLabel => L("Nav_LockVault");

    public string AppTitle => L("App_Title");

    protected override void OnLocalizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnLocalizationPropertyChanged(sender, e);
        RefreshNavigationItems();
        OnPropertyChanged(nameof(LockVaultLabel));
        OnPropertyChanged(nameof(AppTitle));
    }

    public void RefreshNavigationItems()
    {
        var selected = SelectedSection;
        NavigationItems.Clear();
        // Segoe MDL2 Assets glyphs (see Resources/Themes/Icons.xaml)
        NavigationItems.Add(new NavigationItem(AppSection.Keys, L("Nav_Keys"), "\uE8D7", selected == AppSection.Keys));
        NavigationItems.Add(new NavigationItem(AppSection.Generate, L("Nav_Generate"), "\uE710", selected == AppSection.Generate));
        NavigationItems.Add(new NavigationItem(AppSection.Import, L("Nav_Import"), "\uE896", selected == AppSection.Import));
        NavigationItems.Add(new NavigationItem(AppSection.Connections, L("Nav_Connections"), "\uE968", selected == AppSection.Connections));
        NavigationItems.Add(new NavigationItem(AppSection.Settings, L("Nav_Settings"), "\uE713", selected == AppSection.Settings));
    }

    [RelayCommand]
    private void Navigate(AppSection section)
    {
        try
        {
            _navigation.Navigate(section);
            SelectedSection = section;
            RefreshNavigationItems();
        }
        catch (Exception)
        {
            // Navigation failures are logged by the shell host.
        }
    }
}
