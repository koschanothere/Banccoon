using System.Globalization;
using Banccoon.App.Localization;
using Banccoon.Core.ImportExport;

namespace Banccoon.App.Formatting;

// One resx key per entity (and per entity+reference pair) rather than slotting translated nouns
// into a shared template: Russian needs the referenced noun in the accusative with a matching
// adjective ending ("на отсутствующий счёт" vs "на отсутствующую категорию"), which a generic
// "{entity} references missing {reference}" template can't express. The key is built from the
// enum member names (same convention as DisplayText's "Enum_{Type}_{Member}"); a combination with
// no key yet falls back to a generic sentence instead of showing the raw key.
public static class ImportValidationErrorFormatter
{
    public static string Format(ImportValidationError error) => error.Code switch
    {
        ImportValidationErrorCode.UnsupportedFormatVersion => string.Format(
            Translator.Get("ImportValidation_UnsupportedFormatVersion"),
            error.FormatVersion?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
        ImportValidationErrorCode.ApplicationVersionRequired => Translator.Get("ImportValidation_ApplicationVersionRequired"),
        ImportValidationErrorCode.DuplicateId => string.Format(
            GetWithFallback($"ImportValidation_DuplicateId_{error.EntityType}", "ImportValidation_DuplicateId_Generic"),
            error.EntityId),
        ImportValidationErrorCode.MissingReference => string.Format(
            GetWithFallback(
                $"ImportValidation_MissingReference_{error.EntityType}_{error.Reference}",
                "ImportValidation_MissingReference_Generic"),
            error.EntityId,
            error.ReferencedId),
        _ => error.Code.ToString()
    };

    private static string GetWithFallback(string key, string fallbackKey)
    {
        var translated = Translator.Get(key);
        return translated == key
            ? Translator.Get(fallbackKey)
            : translated;
    }
}
