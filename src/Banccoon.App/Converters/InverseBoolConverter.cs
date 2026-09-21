using System.Globalization;

namespace Banccoon.App.Converters;

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolValue && !boolValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolValue && !boolValue;
}
