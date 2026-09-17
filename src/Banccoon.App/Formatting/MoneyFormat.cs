using System.Globalization;

namespace Banccoon.App.Formatting;

public static class MoneyFormat
{
    public static string Format(decimal amount, string currency)
    {
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency)
            ? "EUR"
            : currency.Trim().ToUpperInvariant();
        return $"{normalizedCurrency} {amount.ToString("N2", CultureInfo.InvariantCulture)}";
    }
}
