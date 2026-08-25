using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SshKeyManager.Converters;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var invert = IsInvert(parameter);
        var isNull = value is null || (value is string s && string.IsNullOrWhiteSpace(s));
        var visible = invert ? isNull : !isNull;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool IsInvert(object? parameter) =>
        parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
}
