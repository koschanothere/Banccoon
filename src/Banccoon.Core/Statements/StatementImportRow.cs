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
    Guid? CreatedTransactionId);
