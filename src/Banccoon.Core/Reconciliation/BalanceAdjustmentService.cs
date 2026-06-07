using Banccoon.Core.Models;

namespace Banccoon.Core.Reconciliation;

public sealed class BalanceAdjustmentService : IBalanceAdjustmentService
{
    public Transaction CreateTransaction(BalanceAdjustment adjustment)
    {
        if (adjustment.Difference == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(adjustment), "Balance adjustment difference cannot be zero.");
        }

        var type = adjustment.Difference > 0
            ? TransactionType.Income
            : TransactionType.Expense;
        var direction = adjustment.Difference > 0 ? "up" : "down";

        return new Transaction(
            Guid.NewGuid(),
            adjustment.Date,
            Math.Abs(adjustment.Difference),
            adjustment.AccountId,
            null,
            string.IsNullOrWhiteSpace(adjustment.Notes)
                ? $"Balance adjustment {direction}"
                : adjustment.Notes,
            type);
    }
}
