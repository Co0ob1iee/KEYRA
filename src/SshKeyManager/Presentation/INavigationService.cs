using CommunityToolkit.Mvvm.ComponentModel;
using SshKeyManager.Models;

namespace SshKeyManager.Presentation;

public interface INavigationService
{
    AppSection CurrentSection { get; }

    ObservableObject CurrentViewModel { get; }

    event EventHandler? Navigated;

    void Navigate(AppSection section);

    void Register(AppSection section, Func<ObservableObject> viewModelFactory);
}
