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

    public IStatementParser? FindParser(StatementParseRequest request, IReadOnlyList<string> preferredParserIds)
    {
        ArgumentNullException.ThrowIfNull(request);

        var preferred = preferredParserIds
            .Select(id => parsers.FirstOrDefault(parser => string.Equals(parser.Descriptor.Id, id, StringComparison.OrdinalIgnoreCase)))
            .OfType<IStatementParser>();
        return preferred.FirstOrDefault(parser => parser.CanParse(request)) ?? FindParser(request);
    }
}
