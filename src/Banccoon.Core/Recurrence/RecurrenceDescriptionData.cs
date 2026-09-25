namespace Banccoon.Core.Recurrence;

// Structured recurrence-description data, not a composed sentence - Core must stay
// UI-language-agnostic. Banccoon.App.Formatting.RecurrenceDescriptionFormatter turns this
// into translated display text.
public sealed record RecurrenceDescriptionData(
    RecurrenceFrequency Frequency,
    int Interval,
    bool IsLastDayOfMonth,
    DayOfWeek? ResolvedDayOfWeek,
    int? ResolvedDayOfMonth,
    DateOnly StartDate,
    DateOnly? EndDate);
