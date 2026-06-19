namespace Banccoon.Core.Statements;

public sealed record ParsedStatement(
    string ParserId,
    string ParserName,
    string SourceName,
    IReadOnlyList<ParsedStatementRow> Rows,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null,
    decimal? OpeningBalance = null,
    decimal? ClosingBalance = null);
