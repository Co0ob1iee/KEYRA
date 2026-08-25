using CommunityToolkit.Mvvm.ComponentModel;
using SshKeyManager.Models;
using SshKeyManager.Presentation;

namespace SshKeyManager.Services;

public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<AppSection, Func<ObservableObject>> _factories = new();
    private ObservableObject? _currentViewModel;

    public AppSection CurrentSection { get; private set; } = AppSection.Keys;

    public ObservableObject CurrentViewModel =>
        _currentViewModel ?? throw new InvalidOperationException("Navigation has not been initialized.");

    public event EventHandler? Navigated;

    public void Register(AppSection section, Func<ObservableObject> viewModelFactory)
    {
        ArgumentNullException.ThrowIfNull(viewModelFactory);
        _factories[section] = viewModelFactory;
    }

    public void Navigate(AppSection section)
    {
        if (!_factories.TryGetValue(section, out var factory))
        {
            throw new InvalidOperationException($"Section '{section}' is not registered.");
        }

        try
        {
            CurrentSection = section;
            _currentViewModel = factory();
            Navigated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to navigate to '{section}'.", ex);
        }
    }
}
