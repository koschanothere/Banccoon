namespace Banccoon.Core.Statements;

public sealed record StatementImportCreateResult(
    bool ParserAvailable,
    string Message,
    StatementImportBatch? Batch,
    IReadOnlyList<StatementImportRow> Rows);
