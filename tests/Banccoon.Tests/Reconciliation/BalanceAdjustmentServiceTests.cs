using Banccoon.Core.Models;
using Banccoon.Core.Reconciliation;
using Xunit;

namespace Banccoon.Tests.Reconciliation;

public sealed class BalanceAdjustmentServiceTests
{
    private readonly BalanceAdjustmentService service = new();

    [Fact]
    public void CreateTransaction_WhenDifferenceIsPositive_CreatesIncome()
    {
        var adjustment = new BalanceAdjustment(
            new DateOnly(2026, 6, 7),
            Guid.NewGuid(),
            75m,
            null);

        var transaction = service.CreateTransaction(adjustment);

        Assert.Equal(TransactionType.Income, transaction.Type);
        Assert.Equal(75m, transaction.Amount);
        Assert.Equal("Balance adjustment up", transaction.Notes);
    }

    [Fact]
    public void CreateTransaction_WhenDifferenceIsNegative_CreatesExpense()
    {
        var adjustment = new BalanceAdjustment(
            new DateOnly(2026, 6, 7),
            Guid.NewGuid(),
            -75m,
            "Reality correction");

        var transaction = service.CreateTransaction(adjustment);

        Assert.Equal(TransactionType.Expense, transaction.Type);
        Assert.Equal(75m, transaction.Amount);
        Assert.Equal("Reality correction", transaction.Notes);
    }

    [Fact]
    public void CreateTransaction_WhenDifferenceIsZero_Throws()
    {
        var adjustment = new BalanceAdjustment(
            new DateOnly(2026, 6, 7),
            Guid.NewGuid(),
            0m,
            null);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CreateTransaction(adjustment));
    }
}
