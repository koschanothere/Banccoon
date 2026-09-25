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

    // What Banccoon would suggest right now - type, category and a transfer's other account - for
    // each still-pending row of a batch, from the current learning rules. Rules change as rows are
    // approved, so the review asks again after each approval to fill in the rows the user hasn't
    // touched yet. Read-only: the rows' stored suggestions are left as they were.
    Task<IReadOnlyList<StatementImportRowSuggestion>> GetPendingSuggestionsAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<StatementImportCancelResult> CancelImportAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
}
