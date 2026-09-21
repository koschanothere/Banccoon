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
    int RowCount,
    // The statement's own closing balance (from its most recent operation's running balance, not
    // its header summary line - see SberbankDebitCardStatementParser). Once every row in the batch
    // has been reviewed, this becomes the account's balance directly, superseding whatever the sum
    // of individually-approved transactions computed - the bank's own number is authoritative,
    // and relying on transaction-by-transaction summation alone silently drifts from it whenever a
    // prior transaction was missing, duplicated, or the account's starting balance was off before
    // this import even began.
    decimal? ClosingBalance = null);
