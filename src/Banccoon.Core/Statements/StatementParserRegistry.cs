namespace Banccoon.Core.Statements;

public sealed class StatementParserRegistry : IStatementParserRegistry
{
    private readonly IReadOnlyList<IStatementParser> parsers;

    public StatementParserRegistry(IEnumerable<IStatementParser> parsers)
    {
        this.parsers = parsers.ToArray();
    }

    public IReadOnlyList<StatementParserDescriptor> AvailableParsers => parsers
        .Select(parser => parser.Descriptor)
        .ToArray();

    public IStatementParser? FindParser(StatementParseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return parsers.FirstOrDefault(parser => parser.CanParse(request));
    }
}
