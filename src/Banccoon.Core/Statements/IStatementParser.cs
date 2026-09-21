namespace Banccoon.Core.Statements;

public interface IStatementParser
{
    StatementParserDescriptor Descriptor { get; }

    bool CanParse(StatementParseRequest request);

    Task<ParsedStatement> ParseAsync(
        StatementParseRequest request,
        CancellationToken cancellationToken = default);
}
