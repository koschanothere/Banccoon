namespace Banccoon.Core.Reconciliation;

public sealed record ReconciliationResult(
    DateOnly Date,
    decimal ExpectedBalance,
    decimal ActualBalance,
    decimal Difference,
    ReconciliationStatus Status);
