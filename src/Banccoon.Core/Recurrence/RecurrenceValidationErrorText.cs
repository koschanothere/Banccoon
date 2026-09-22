namespace Banccoon.Core.Recurrence;

// English-only text for RecurrenceValidationErrorCode, for contexts that haven't been migrated to
// real translation yet: RecurrenceValidationException's diagnostic Message (developer/log-facing,
// deliberately not translated - see that class), and RecurrenceSyntaxService.TryParse's Errors
// list (translating syntax errors needs its own pass - they interleave with untranslatable
// technical tokens like "FREQ"/"BYMONTHDAY" - see the i18n scoping note in
// docs/development-phases.md).
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
