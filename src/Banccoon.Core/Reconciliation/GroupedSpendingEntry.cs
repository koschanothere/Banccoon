namespace Banccoon.Core.Reconciliation;

public sealed record GroupedSpendingEntry(
    DateOnly Date,
    decimal Amount,
    Guid AccountId,
    Guid? CategoryId,
    string? Notes);
