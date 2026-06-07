namespace Banccoon.Core.Reconciliation;

public sealed record CheckInSession(
    Guid Id,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<ExpectedTransactionReview> ExpectedTransactions);
