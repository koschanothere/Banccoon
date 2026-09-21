using System.Globalization;

namespace Banccoon.App.Converters;

public sealed class PositiveAmountColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isPositive = value is true;
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        if (isPositive)
        {
            return theme == AppTheme.Dark ? Color.FromArgb("#34D399") : Color.FromArgb("#16A34A");
        }

        return theme == AppTheme.Dark ? Color.FromArgb("#F2F3F5") : Color.FromArgb("#181A1D");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
