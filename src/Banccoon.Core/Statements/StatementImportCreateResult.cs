namespace Banccoon.Core.Statements;

public sealed record StatementImportCreateResult(
    bool ParserAvailable,
    StatementImportMessage Message,
    StatementImportBatch? Batch,
    IReadOnlyList<StatementImportRow> Rows);
