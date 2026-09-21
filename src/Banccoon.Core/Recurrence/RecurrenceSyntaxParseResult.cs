namespace Banccoon.Core.Recurrence;

public sealed record RecurrenceSyntaxParseResult(RecurrenceRule? Rule, IReadOnlyList<string> Errors)
{
    public bool IsValid => Rule is not null && Errors.Count == 0;

    public static RecurrenceSyntaxParseResult Success(RecurrenceRule rule)
    {
        return new RecurrenceSyntaxParseResult(rule, Array.Empty<string>());
    }

    public static RecurrenceSyntaxParseResult Failure(IReadOnlyList<string> errors)
    {
        return new RecurrenceSyntaxParseResult(null, errors);
    }
}
