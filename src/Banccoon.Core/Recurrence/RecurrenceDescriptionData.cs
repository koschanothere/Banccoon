namespace Banccoon.Core.Recurrence;

public sealed record RecurrenceDescriptionData(
    RecurrenceFrequency Frequency,
    int Interval,
    bool IsLastDayOfMonth,
    DayOfWeek? ResolvedDayOfWeek,
    int? ResolvedDayOfMonth,
    DateOnly StartDate,
    DateOnly? EndDate);
