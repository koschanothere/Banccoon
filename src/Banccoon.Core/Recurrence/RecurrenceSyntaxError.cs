namespace Banccoon.Core.Recurrence;

// Core stays UI-language-agnostic: no composed sentence here, just what went wrong (Code) plus
// the raw technical text it concerns (Token - a field name like "FREQ"/"BYMONTHDAY", or the
// malformed "KEY=VALUE" fragment itself). Token is always passed through verbatim by the App
// layer's formatter as a placeholder argument, never translated or baked into a translated string.
public sealed record RecurrenceSyntaxError(
    RecurrenceSyntaxErrorCode Code,
    string? Token = null,
    RecurrenceValidationErrorCode? ValidationError = null)
{
    public static RecurrenceSyntaxError FromValidation(RecurrenceValidationErrorCode code)
    {
        return new RecurrenceSyntaxError(RecurrenceSyntaxErrorCode.RuleInvalid, ValidationError: code);
    }
}
