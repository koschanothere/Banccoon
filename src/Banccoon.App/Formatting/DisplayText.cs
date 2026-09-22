using Banccoon.App.Localization;

namespace Banccoon.App.Formatting;

public static class DisplayText
{
    // Enum values look up "Enum_{TypeName}_{MemberName}" (e.g. "Enum_AccountType_DebitCard") and
    // fall back to splitting the raw member name (e.g. "Debit Card") when no translation exists
    // yet - so adding resx coverage for an enum is a content-only change, never a code change,
    // and an enum nobody has translated yet keeps behaving exactly as before.
    public static string Format(object value)
    {
        if (value is Enum enumValue)
        {
            var key = $"Enum_{enumValue.GetType().Name}_{enumValue}";
            var translated = Translator.Get(key);
            if (translated != key)
            {
                return translated;
            }
        }

        return value.ToString() is { } text
            ? SplitPascalCase(text)
            : string.Empty;
    }

    private static string SplitPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var result = new List<char>(text.Length + 4) { text[0] };
        for (var index = 1; index < text.Length; index++)
        {
            var current = text[index];
            var previous = text[index - 1];
            if (char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous)))
            {
                result.Add(' ');
            }

            result.Add(current);
        }

        return new string(result.ToArray());
    }
}
