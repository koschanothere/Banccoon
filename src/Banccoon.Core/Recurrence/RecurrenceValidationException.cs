namespace Banccoon.Core.Recurrence;

public sealed class RecurrenceValidationException : ArgumentException
{
    // This exception's Message stays English-only (dev/log-facing - it only fires when a rule that
    // already failed UI-level validation reaches Core anyway, since RecurrenceEditorViewModel gates
    // on IsValid before calling anything that can throw this, so normal usage never surfaces it to
    // a user), same as every other ArgumentException in Core.
    public RecurrenceValidationException(IReadOnlyList<RecurrenceValidationErrorCode> errors)
        : base(string.Join(Environment.NewLine, errors.Select(RecurrenceValidationErrorText.Describe)))
    {
        Errors = errors;
    }

    public IReadOnlyList<RecurrenceValidationErrorCode> Errors { get; }
}
