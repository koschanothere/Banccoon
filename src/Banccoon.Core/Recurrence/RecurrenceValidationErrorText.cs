namespace Banccoon.Core.Recurrence;

// English-only text for RecurrenceValidationErrorCode, used only by RecurrenceValidationException's
// diagnostic Message (developer/log-facing, deliberately not translated - see that class). Every
// user-facing path (RecurrenceEditorViewModel, including syntax errors via RecurrenceSyntaxError)
// goes through the App layer's Translator-backed formatters instead.
internal static class RecurrenceValidationErrorText
{
    public static string Describe(RecurrenceValidationErrorCode code) => code switch
    {
        RecurrenceValidationErrorCode.IntervalTooLow => "Recurrence interval must be at least 1.",
        RecurrenceValidationErrorCode.EndDateBeforeStartDate => "Recurrence end date must be on or after the start date.",
        RecurrenceValidationErrorCode.DayOfMonthOutOfRange => "Day of month must be between 1 and 31.",
        RecurrenceValidationErrorCode.MonthlyDayOutOfRange => "Monthly recurrence day must be between 1 and 31.",
        _ => code.ToString()
    };
}
