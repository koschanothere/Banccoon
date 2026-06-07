namespace Banccoon.Core.Recurrence;

public sealed record RecurrenceRule(
    RecurrenceFrequency Frequency,
    int Interval,
    DateOnly StartDate,
    DateOnly? EndDate = null,
    DayOfWeek? DayOfWeek = null,
    int? DayOfMonth = null,
    MonthlyRecurrenceMode MonthlyMode = MonthlyRecurrenceMode.DayOfMonth);
