using Banccoon.App.Localization;

namespace Banccoon.App.Formatting;

public static class AccountNumberFormat
{
    public static string Format(string? value)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return GroupInFours(normalized);
    }

    public static string Mask(string? value)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Translator.Get("AccountNumber_Hidden");
        }

        var visibleDigits = normalized.Length <= 4
            ? normalized
            : normalized[^4..];

        return $"**** {visibleDigits}";
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Concat(value.Where(char.IsDigit));
    }

    private static string GroupInFours(string normalized)
    {
        return string.Join(
            ' ',
            Enumerable.Range(0, (normalized.Length + 3) / 4)
                .Select(index =>
                {
                    var start = index * 4;
                    var length = Math.Min(4, normalized.Length - start);
                    return normalized.Substring(start, length);
                }));
    }
}
