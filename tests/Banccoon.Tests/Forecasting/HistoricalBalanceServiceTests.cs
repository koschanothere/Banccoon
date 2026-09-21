using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Xunit;

namespace Banccoon.Tests.Forecasting;

public sealed class HistoricalBalanceServiceTests
{
    private readonly HistoricalBalanceService service = new();

    [Fact]
    public void GetHistoricalBalances_WithNoTransactions_ReturnsFlatLineAtCurrentBalance()
    {
        var accountId = Guid.NewGuid();
        var points = service.GetHistoricalBalances(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 3),
            100m,
            new[] { accountId },
            Array.Empty<Transaction>());

        Assert.Equal(3, points.Count);
        Assert.All(points, point => Assert.Equal(100m, point.Balance));
    }

    [Fact]
    public void GetHistoricalBalances_ReconstructsBalanceBeforeAnExpense()
    {
        var accountId = Guid.NewGuid();
        var expense = CreateTransaction(
            new DateOnly(2026, 6, 3),
            20m,
            TransactionType.Expense,
            accountId);

        var points = service.GetHistoricalBalances(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 3),
            100m,
            new[] { accountId },
            new[] { expense });

        Assert.Equal(120m, points[0].Balance);
        Assert.Equal(120m, points[1].Balance);
        Assert.Equal(100m, points[2].Balance);
    }

    [Fact]
    public void GetHistoricalBalances_TransferBetweenTwoIncludedAccounts_DoesNotChangeTotal()
    {
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var transfer = CreateTransaction(
            new DateOnly(2026, 6, 2),
            50m,
            TransactionType.Transfer,
            accountA,
            accountB);

        var points = service.GetHistoricalBalances(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 3),
            200m,
            new[] { accountA, accountB },
            new[] { transfer });

        Assert.All(points, point => Assert.Equal(200m, point.Balance));
    }

    [Fact]
    public void GetHistoricalBalances_TransferToExcludedAccount_ReducesTotalGoingForward()
    {
        var includedAccount = Guid.NewGuid();
        var excludedAccount = Guid.NewGuid();
        var transfer = CreateTransaction(
            new DateOnly(2026, 6, 2),
            50m,
            TransactionType.Transfer,
            includedAccount,
            excludedAccount);

        var points = service.GetHistoricalBalances(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 3),
            150m,
            new[] { includedAccount },
            new[] { transfer });

        Assert.Equal(200m, points[0].Balance);
        Assert.Equal(150m, points[1].Balance);
        Assert.Equal(150m, points[2].Balance);
    }

    [Fact]
    public void GetHistoricalBalances_TransactionOnExcludedAccount_HasNoEffect()
    {
        var includedAccount = Guid.NewGuid();
        var excludedAccount = Guid.NewGuid();
        var expense = CreateTransaction(
            new DateOnly(2026, 6, 2),
            20m,
            TransactionType.Expense,
            excludedAccount);

        var points = service.GetHistoricalBalances(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 3),
            100m,
            new[] { includedAccount },
            new[] { expense });

        Assert.All(points, point => Assert.Equal(100m, point.Balance));
    }

    private static Transaction CreateTransaction(
        DateOnly date,
        decimal amount,
        TransactionType type,
        Guid accountId,
        Guid? destinationAccountId = null)
    {
        return new Transaction(
            Guid.NewGuid(),
            date,
            amount,
            accountId,
            CategoryId: null,
            Notes: null,
            type,
            destinationAccountId);
    }
}
