using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SshKeyManager.Services;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly CultureInfo EnglishFallback = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo DefaultCulture = CultureInfo.GetCultureInfo("pl-PL");

    private readonly ResourceManager _resources = new(
        "SshKeyManager.Resources.Strings",
        typeof(LocalizationService).Assembly);

    private readonly Dictionary<string, Dictionary<string, string>> _jsonLocales = new(StringComparer.OrdinalIgnoreCase);

    private CultureInfo _currentCulture = DefaultCulture;

    public LocalizationService()
    {
        LoadJsonLocales();
    }

    public CultureInfo CurrentCulture => _currentCulture;

    public string this[string key] => GetString(key);

    public event PropertyChangedEventHandler? PropertyChanged;

    public string GetString(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (TryGetFromJson(_currentCulture, key, out var jsonValue))
        {
            return jsonValue;
        }

        var value = _resources.GetString(key, _currentCulture);
        if (value is null && !CultureMatches(_currentCulture, EnglishFallback))
        {
            if (TryGetFromJson(EnglishFallback, key, out jsonValue))
            {
                return jsonValue;
            }

            value = _resources.GetString(key, EnglishFallback);
        }

        if (value is null && !CultureMatches(_currentCulture, DefaultCulture))
        {
            if (TryGetFromJson(DefaultCulture, key, out jsonValue))
            {
                return jsonValue;
            }

            value = _resources.GetString(key, DefaultCulture);
        }

        return value ?? key;
    }

    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(_currentCulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    public void SetCulture(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            culture = DefaultCulture;
        }

        if (_currentCulture.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentCulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
    }

    public string FormatDateTime(DateTime dateTime) =>
        dateTime.ToString("g", _currentCulture);

    public string FormatDateTimeUtc(DateTime dateTimeUtc) =>
        FormatDateTime(dateTimeUtc.ToLocalTime());

    private void LoadJsonLocales()
    {
        var assembly = typeof(LocalizationService).Assembly;
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".locale.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cultureKey = resourceName.Split('.')[^3];
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    continue;
                }

                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
                if (dict is not null)
                {
                    _jsonLocales[cultureKey] = dict;
                }
            }
            catch (Exception)
            {
                // Ignore malformed locale files.
            }
        }
    }

    private bool TryGetFromJson(CultureInfo culture, string key, out string value)
    {
        value = string.Empty;
        if (_jsonLocales.TryGetValue(culture.Name, out var exact) && exact.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }

        var twoLetter = culture.TwoLetterISOLanguageName;
        foreach (var pair in _jsonLocales)
        {
            if (!pair.Key.StartsWith(twoLetter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (pair.Value.TryGetValue(key, out v))
            {
                value = v;
                return true;
            }
        }

        return false;
    }

    private static bool CultureMatches(CultureInfo culture, CultureInfo target) =>
        culture.Name.Equals(target.Name, StringComparison.OrdinalIgnoreCase) ||
        culture.TwoLetterISOLanguageName.Equals(target.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase);
}
