using System.Globalization;

namespace Banccoon.App.Converters;

// The current month's bar in a category sparkline uses the accent color; the historical months
// use a muted divider-like tone, so the "where are we now" bar reads at a glance.
public sealed class CurrentBarColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isCurrent = value is true;
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        if (isCurrent)
        {
            return theme == AppTheme.Dark ? Color.FromArgb("#10B981") : Color.FromArgb("#059669");
        }

        return theme == AppTheme.Dark ? Color.FromArgb("#363B42") : Color.FromArgb("#E7E9EC");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
