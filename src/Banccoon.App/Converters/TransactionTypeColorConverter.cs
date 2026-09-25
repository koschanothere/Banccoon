using System.Globalization;
using Banccoon.Core.Models;

namespace Banccoon.App.Converters;

// The one amount-colour rule for anything with a TransactionType, used everywhere a typed amount is
// shown (Transactions list, resolve-upcoming rows, statement import review): income green,
// transfer purple, expense the normal text colour. Same hex values as the LightStatusGreen /
// LightTransfer (and Dark*) tokens in App.xaml, resolved from the live theme at convert time like
// the other amount converters.
public sealed class TransactionTypeColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDark = (Application.Current?.RequestedTheme ?? AppTheme.Light) == AppTheme.Dark;
        return value switch
        {
            TransactionType.Income => isDark ? Color.FromArgb("#34D399") : Color.FromArgb("#16A34A"),
            TransactionType.Transfer => isDark ? Color.FromArgb("#C084FC") : Color.FromArgb("#9333EA"),
            _ => isDark ? Color.FromArgb("#F2F3F5") : Color.FromArgb("#181A1D")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
