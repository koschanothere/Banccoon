namespace Banccoon.Core.Recurrence;

public sealed record RecurrenceSyntaxParseResult(RecurrenceRule? Rule, IReadOnlyList<RecurrenceSyntaxError> Errors)
{
    public bool IsValid => Rule is not null && Errors.Count == 0;

    public static RecurrenceSyntaxParseResult Success(RecurrenceRule rule)
    {
        return new RecurrenceSyntaxParseResult(rule, Array.Empty<RecurrenceSyntaxError>());
    }

    public static RecurrenceSyntaxParseResult Failure(IReadOnlyList<RecurrenceSyntaxError> errors)
    {
        return new RecurrenceSyntaxParseResult(null, errors);
    }
}
