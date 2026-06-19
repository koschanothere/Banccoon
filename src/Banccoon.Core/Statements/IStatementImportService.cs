namespace Banccoon.Core.Statements;

public interface IStatementImportService
{
    Task<StatementPreviewResult> PreviewAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<StatementImportCreateResult> CreatePendingImportAsync(
        Guid accountId,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<StatementImportCreateResult> CreatePendingImportAsync(
        Guid accountId,
        string filePath,
        ParsedStatement parsedStatement,
        CancellationToken cancellationToken = default);

    Task<StatementRowImportResult> ApproveRowAsync(
        Guid rowId,
        Guid? categoryId,
        CancellationToken cancellationToken = default);

    Task<StatementImportRow> SkipRowAsync(
        Guid rowId,
        CancellationToken cancellationToken = default);

    Task<StatementImportCancelResult> CancelImportAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
}
