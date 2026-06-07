using Banccoon.Core.Models;
using Banccoon.Core.Reconciliation;
using Xunit;

namespace Banccoon.Tests.Reconciliation;

public sealed class GroupedSpendingServiceTests
{
    private readonly GroupedSpendingService service = new();

    [Fact]
    public void CreateTransaction_CreatesExpenseTransaction()
    {
        var accountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var entry = new GroupedSpendingEntry(
            new DateOnly(2026, 6, 7),
            120m,
            accountId,
            categoryId,
            "Food and entertainment");

        var transaction = service.CreateTransaction(entry);

        Assert.Equal(TransactionType.Expense, transaction.Type);
        Assert.Equal(120m, transaction.Amount);
        Assert.Equal(accountId, transaction.AccountId);
        Assert.Equal(categoryId, transaction.CategoryId);
        Assert.Equal("Food and entertainment", transaction.Notes);
    }

    [Fact]
    public void CreateTransaction_WhenAmountIsNotPositive_Throws()
    {
        var entry = new GroupedSpendingEntry(
            new DateOnly(2026, 6, 7),
            0m,
            Guid.NewGuid(),
            null,
            null);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CreateTransaction(entry));
    }
}
