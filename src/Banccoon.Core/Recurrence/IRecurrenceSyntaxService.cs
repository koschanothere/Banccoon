namespace Banccoon.Core.Recurrence;

public interface IRecurrenceSyntaxService
{
    string Format(RecurrenceRule rule);

    RecurrenceSyntaxParseResult TryParse(string syntax);

    IReadOnlyList<RecurrenceSyntaxExample> GetExamples();
}
