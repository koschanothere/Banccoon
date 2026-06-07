namespace Banccoon.Core.Models;

public sealed record Transaction(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    Guid AccountId,
    Guid? CategoryId,
    string? Notes,
    TransactionType Type);
