namespace Banccoon.Core.Statements;

public sealed record StatementImportBatch(
    Guid Id,
    Guid AccountId,
    string ParserId,
    string ParserName,
    string SourceFileName,
    string? SourceFilePath,
    DateTimeOffset ImportedAt,
    StatementImportBatchStatus Status,
    int RowCount);
