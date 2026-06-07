namespace Banccoon.Core.Recurrence;

public sealed record RecurrenceValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static RecurrenceValidationResult Success { get; } = new(Array.Empty<string>());
}
