using Banccoon.App.Localization;
using Banccoon.Core.Recurrence;

namespace Banccoon.App.Formatting;

public static class RecurrenceValidationMessageFormatter
{
    public static string Format(RecurrenceValidationErrorCode code) => code switch
    {
        RecurrenceValidationErrorCode.IntervalTooLow => Translator.Get("RecurrenceValidation_IntervalTooLow"),
        RecurrenceValidationErrorCode.EndDateBeforeStartDate => Translator.Get("RecurrenceValidation_EndDateBeforeStartDate"),
        RecurrenceValidationErrorCode.DayOfMonthOutOfRange => Translator.Get("RecurrenceValidation_DayOfMonthOutOfRange"),
        RecurrenceValidationErrorCode.MonthlyDayOutOfRange => Translator.Get("RecurrenceValidation_MonthlyDayOutOfRange"),
        _ => code.ToString()
    };
}
