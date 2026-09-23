using Banccoon.Core.Models;

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
        TransactionType? type,
        Guid? destinationAccountId,
        CancellationToken cancellationToken = default);

    Task<StatementImportRow> SkipRowAsync(
        Guid rowId,
        CancellationToken cancellationToken = default);

    // Puts an approved or skipped row back to Pending. For an approved row, its transaction is
    // deleted and its balance effect reversed. Only while the batch is still under review: once
    // every row is reviewed, the statement's closing balance has already been applied to the
    // account, and that can't be cleanly unwound one row at a time.
    Task<StatementImportRow> UndoReviewAsync(
        Guid rowId,
        CancellationToken cancellationToken = default);

    Task<StatementImportCancelResult> CancelImportAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
}
