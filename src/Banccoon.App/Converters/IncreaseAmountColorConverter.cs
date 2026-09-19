using System.Globalization;

namespace Banccoon.App.Converters;

// Flags a category's spend going UP as worth attention (red) - the opposite polarity from
// PositiveAmountColorConverter, since "positive" for a transaction amount is good, but an
// increase in spend is the one worth calling out. A decrease uses the same neutral text color as
// "no change" - the row's own change text already carries the +/- sign and percentage.
public sealed class IncreaseAmountColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isIncrease = value is true;
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        if (isIncrease)
        {
            return theme == AppTheme.Dark ? Color.FromArgb("#F87171") : Color.FromArgb("#DC2626");
        }

        return theme == AppTheme.Dark ? Color.FromArgb("#F2F3F5") : Color.FromArgb("#181A1D");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
