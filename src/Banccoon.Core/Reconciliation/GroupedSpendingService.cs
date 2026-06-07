using Banccoon.Core.Models;

namespace Banccoon.Core.Reconciliation;

public sealed class GroupedSpendingService : IGroupedSpendingService
{
    public Transaction CreateTransaction(GroupedSpendingEntry entry)
    {
        if (entry.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entry), "Grouped spending amount must be greater than zero.");
        }

        return new Transaction(
            Guid.NewGuid(),
            entry.Date,
            Math.Abs(entry.Amount),
            entry.AccountId,
            entry.CategoryId,
            string.IsNullOrWhiteSpace(entry.Notes) ? "Grouped spending" : entry.Notes,
            TransactionType.Expense);
    }
}
