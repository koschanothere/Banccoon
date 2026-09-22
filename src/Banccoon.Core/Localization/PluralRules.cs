namespace Banccoon.Core.Localization;

// Determines which grammatical plural form a count needs, per language - not display text itself
// (Core stays UI-language-agnostic; the App layer maps a PluralForm to a resx-backed string).
public static class PluralRules
{
    // languageCode is a two-letter code matching AppSettings.DisplayLanguage ("en", "ru", ...).
    // Anything not explicitly handled falls back to English's two-form rule.
    public static PluralForm GetForm(int count, string languageCode)
    {
        return languageCode switch
        {
            "ru" => GetRussianForm(count),
            _ => GetEnglishForm(count)
        };
    }

    private static PluralForm GetEnglishForm(int count)
    {
        return count == 1 ? PluralForm.One : PluralForm.Other;
    }

    // CLDR cardinal rules for Russian (and other East Slavic languages), integers only:
    //   one:  n%10=1  and n%100!=11             -> 1, 21, 31, 41, ... (not 11)
    //   few:  n%10=2..4 and n%100 not in 12..14  -> 2-4, 22-24, 32-34, ... (not 12-14)
    //   many: everything else                    -> 0, 5-20, 25-30, 100, 111-114, ...
    private static PluralForm GetRussianForm(int count)
    {
        var n = Math.Abs(count);
        var lastDigit = n % 10;
        var lastTwoDigits = n % 100;

        if (lastDigit == 1 && lastTwoDigits != 11)
        {
            return PluralForm.One;
        }

        if (lastDigit is >= 2 and <= 4 && (lastTwoDigits < 12 || lastTwoDigits > 14))
        {
            return PluralForm.Few;
        }

        return PluralForm.Many;
    }
}
