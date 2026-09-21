namespace Banccoon.Core.Reconciliation;

public sealed record BalanceAdjustment(
    DateOnly Date,
    Guid AccountId,
    decimal Difference,
    string? Notes);
