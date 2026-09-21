using System.Globalization;

namespace Banccoon.App.Converters;

// Highlights the selected row in the Settings page's category nav list - reuses the same
// accent-soft tokens (LightAccentSoft/DarkAccentSoft) already used for the accent color elsewhere,
// so the highlight reads as "this app's selection color," not a one-off.
public sealed class SelectedNavBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = value is true;
        if (!isSelected)
        {
            return Colors.Transparent;
        }

        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        return theme == AppTheme.Dark ? Color.FromArgb("#2910B981") : Color.FromArgb("#1A059669");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
