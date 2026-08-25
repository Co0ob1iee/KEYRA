using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SshKeyManager.Services;

namespace SshKeyManager.ViewModels;

public abstract class LocalizedViewModelBase : ObservableObject
{
    protected LocalizedViewModelBase(ILocalizationService localization)
    {
        Localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Localization.PropertyChanged += OnLocalizationPropertyChanged;
    }

    protected ILocalizationService Localization { get; }

    protected string L(string key) => Localization.GetString(key);

    protected string L(string key, params object[] args) => Localization.GetString(key, args);

    protected virtual void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            OnPropertyChanged(string.Empty);
            return;
        }

        OnLocalizationChanged(e.PropertyName);
    }

    protected virtual void OnLocalizationChanged(string key)
    {
    }
}
