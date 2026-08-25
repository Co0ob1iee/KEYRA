using System.ComponentModel;
using System.Globalization;

namespace SshKeyManager.Services;

public interface ILocalizationService : INotifyPropertyChanged
{
    CultureInfo CurrentCulture { get; }

    string this[string key] { get; }

    string GetString(string key);

    string GetString(string key, params object[] args);

    void SetCulture(string cultureName);

    string FormatDateTime(DateTime dateTime);

    string FormatDateTimeUtc(DateTime dateTimeUtc);
}
