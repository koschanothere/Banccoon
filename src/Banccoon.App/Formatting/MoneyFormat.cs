using System.Globalization;

namespace Banccoon.App.Formatting;

public static class MoneyFormat
{
    public static string Format(decimal amount, string currency)
    {
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency)
            ? "EUR"
            : currency.Trim().ToUpperInvariant();

        if (PrivacyMode.IsEnabled)
        {
            // Currency code stays visible (far less revealing than the amount itself); the sign
            // is preserved so red/green amount coloring elsewhere in the app still reads correctly.
            return $"{normalizedCurrency} {(amount < 0m ? "-" : string.Empty)}••••";
        }

        return $"{normalizedCurrency} {amount.ToString("N2", CultureInfo.InvariantCulture)}";
    }
}
