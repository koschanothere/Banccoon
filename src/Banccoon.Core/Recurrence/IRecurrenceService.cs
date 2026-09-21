namespace Banccoon.Core.Recurrence;

public interface IRecurrenceService
{
    IReadOnlyList<DateOnly> GetOccurrences(RecurrenceRule rule, DateOnly fromInclusive, DateOnly toInclusive);

    DateOnly? GetNextOccurrence(RecurrenceRule rule, DateOnly afterDate);

    RecurrenceDescriptionData Describe(RecurrenceRule rule);
}
