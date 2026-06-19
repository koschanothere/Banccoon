using Banccoon.Core.Models;

namespace Banccoon.Core.Statements;

public sealed record ParsedStatementRow(
    DateOnly Date,
    decimal Amount,
    TransactionType Type,
    string Description,
    string? Counterparty = null,
    string? ExternalReference = null,
    string? RawText = null);
