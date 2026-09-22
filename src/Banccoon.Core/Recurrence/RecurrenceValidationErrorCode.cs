namespace Banccoon.Core.Recurrence;

public enum RecurrenceValidationErrorCode
{
    IntervalTooLow,
    EndDateBeforeStartDate,
    DayOfMonthOutOfRange,
    MonthlyDayOutOfRange
}
