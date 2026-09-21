namespace Banccoon.Core.Statements;

public interface IStatementParserRegistry
{
    IReadOnlyList<StatementParserDescriptor> AvailableParsers { get; }

    IStatementParser? FindParser(StatementParseRequest request);
}
