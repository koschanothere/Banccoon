namespace Banccoon.Core.Recurrence;

public interface IRecurrenceDescriptionService
{
    RecurrenceDescriptionData Describe(RecurrenceRule rule);
}
