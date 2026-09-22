namespace Banccoon.Core.Recurrence;

public sealed record RecurrenceValidationResult(IReadOnlyList<RecurrenceValidationErrorCode> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static RecurrenceValidationResult Success { get; } = new(Array.Empty<RecurrenceValidationErrorCode>());
}
