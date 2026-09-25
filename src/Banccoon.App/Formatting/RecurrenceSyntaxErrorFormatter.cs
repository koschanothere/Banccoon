using Banccoon.App.Localization;
using Banccoon.Core.Recurrence;

namespace Banccoon.App.Formatting;

// error.Token (a raw field name like "FREQ", or the malformed "KEY=VALUE" fragment itself) is
// always substituted in as a {0} placeholder argument, never translated - the parser only accepts
// those literal English keywords regardless of UI language.
public static class RecurrenceSyntaxErrorFormatter
{
    public static string Format(RecurrenceSyntaxError error) => error.Code switch
    {
        RecurrenceSyntaxErrorCode.Empty => Translator.Get("RecurrenceSyntaxError_Empty"),
        RecurrenceSyntaxErrorCode.InvalidField => FormatWithToken("RecurrenceSyntaxError_InvalidField", error),
        RecurrenceSyntaxErrorCode.FieldRequired => FormatWithToken("RecurrenceSyntaxError_FieldRequired", error),
        RecurrenceSyntaxErrorCode.UnsupportedValue => FormatWithToken("RecurrenceSyntaxError_UnsupportedValue", error),
        RecurrenceSyntaxErrorCode.NotWholeNumber => FormatWithToken("RecurrenceSyntaxError_NotWholeNumber", error),
        RecurrenceSyntaxErrorCode.InvalidMonthDay => FormatWithToken("RecurrenceSyntaxError_InvalidMonthDay", error),
        RecurrenceSyntaxErrorCode.RuleInvalid when error.ValidationError is { } validationError =>
            RecurrenceValidationMessageFormatter.Format(validationError),
        _ => Translator.Get("RecurrenceEditor_SyntaxNotParsed")
    };

    private static string FormatWithToken(string key, RecurrenceSyntaxError error)
    {
        return string.Format(Translator.Get(key), error.Token ?? string.Empty);
    }
}
