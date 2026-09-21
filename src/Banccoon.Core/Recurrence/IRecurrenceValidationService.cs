namespace Banccoon.Core.Recurrence;

public interface IRecurrenceValidationService
{
    RecurrenceValidationResult Validate(RecurrenceRule rule);

    void ThrowIfInvalid(RecurrenceRule rule);
}
