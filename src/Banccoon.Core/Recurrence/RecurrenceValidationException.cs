namespace Banccoon.Core.Recurrence;

public sealed class RecurrenceValidationException : ArgumentException
{
    public RecurrenceValidationException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
