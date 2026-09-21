using Banccoon.Core.Models;

namespace Banccoon.Core.Statements;

public sealed record StatementImportRow(
    Guid Id,
    Guid BatchId,
    DateOnly Date,
    decimal Amount,
    TransactionType Type,
    string Description,
    string NormalizedDescription,
    string? Counterparty,
    string? ExternalReference,
    string? RawText,
    Guid? SuggestedCategoryId,
    Guid? CategoryId,
    StatementImportRowStatus Status,
    bool IsDuplicate,
    Guid? DuplicateTransactionId,
    Guid? CreatedTransactionId,
    TimeOnly? Time = null,
    // The other account involved once Type is Transfer - NOT necessarily the semantic
    // destination. If IsIncoming is true, this account is actually the transfer's source and the
    // batch's own account is the destination (see StatementImportService.ApproveRowAsync).
    Guid? DestinationAccountId = null,
    // Whether the original statement showed this as money arriving (true) or leaving (false),
    // captured once from the parser's raw +/- sign and preserved even if Type is later
    // reclassified to Transfer - Transfer alone doesn't say which way the money moved.
    bool IsIncoming = false);
