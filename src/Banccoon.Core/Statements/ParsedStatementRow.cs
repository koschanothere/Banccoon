using Banccoon.Core.Models;

namespace Banccoon.Core.Statements;

public sealed record ParsedStatementRow(
    DateOnly Date,
    decimal Amount,
    TransactionType Type,
    string Description,
    string? Counterparty = null,
    string? ExternalReference = null,
    string? RawText = null,
    decimal? BalanceAfter = null,
    TimeOnly? Time = null,
    // The bank's own category for the operation (e.g. Sberbank's "Супермаркеты"), when its
    // statements show one - see BankCategoryLink.
    string? BankCategory = null);
