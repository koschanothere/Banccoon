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

    [Fact]
    public void GetHistoricalBalances_WithNoTransactions_PointsCarryNoEvents()
    {
        var points = service.GetHistoricalBalances(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 3),
            100m,
            new[] { Guid.NewGuid() },
            Array.Empty<Transaction>());

        Assert.All(points, point => Assert.Empty(point.Events));
    }

    [Fact]
    public void GetHistoricalBalances_ReportsEachDaysTransactionsOnThatDaysPoint()
    {
        var accountId = Guid.NewGuid();
        var groceries = CreateTransaction(new DateOnly(2026, 6, 2), 30m, TransactionType.Expense, accountId, name: "Groceries");
        var salary = CreateTransaction(new DateOnly(2026, 6, 3), 500m, TransactionType.Income, accountId, name: "Salary");

        var points = service.GetHistoricalBalances(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 3),
            1000m,
            new[] { accountId },
            new[] { groceries, salary });

        Assert.Empty(points[0].Events);
        var groceriesEvent = Assert.Single(points[1].Events);
        Assert.Equal(new HistoricalBalanceEvent(groceries.Id, "Groceries", TransactionType.Expense, 30m, -30m), groceriesEvent);
        var salaryEvent = Assert.Single(points[2].Events);
        Assert.Equal(new HistoricalBalanceEvent(salary.Id, "Salary", TransactionType.Income, 500m, 500m), salaryEvent);
    }

    [Fact]
    public void GetHistoricalBalances_ReportsStartDatesOwnTransactionsWithoutReversingThem()
    {
        var accountId = Guid.NewGuid();
        var coffee = CreateTransaction(new DateOnly(2026, 6, 1), 5m, TransactionType.Expense, accountId, name: "Coffee");

        var points = service.GetHistoricalBalances(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 2),
            100m,
            new[] { accountId },
            new[] { coffee });

        // The start day's end-of-day balance already includes its own expense.
        Assert.Equal(100m, points[0].Balance);
        Assert.Equal("Coffee", Assert.Single(points[0].Events).Name);
    }

    [Fact]
    public void GetHistoricalBalances_IgnoresTransactionsOutsideTheRangeAndOnExcludedAccounts()
    {
        var includedAccount = Guid.NewGuid();
        var excludedAccount = Guid.NewGuid();
        var beforeRange = CreateTransaction(new DateOnly(2026, 5, 31), 10m, TransactionType.Expense, includedAccount);
        var afterRange = CreateTransaction(new DateOnly(2026, 6, 4), 10m, TransactionType.Expense, includedAccount);
        var onExcluded = CreateTransaction(new DateOnly(2026, 6, 2), 10m, TransactionType.Expense, excludedAccount);

        var points = service.GetHistoricalBalances(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 3),
            100m,
            new[] { includedAccount },
            new[] { beforeRange, afterRange, onExcluded });

        Assert.All(points, point => Assert.Empty(point.Events));
    }

    [Fact]
    public void GetHistoricalBalances_TransferBetweenIncludedAccounts_IsReportedWithZeroTotalEffect()
    {
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var transfer = CreateTransaction(new DateOnly(2026, 6, 2), 50m, TransactionType.Transfer, accountA, accountB, "To savings");

        var points = service.GetHistoricalBalances(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 2),
            200m,
            new[] { accountA, accountB },
            new[] { transfer });

        var transferEvent = Assert.Single(points[1].Events);
        Assert.Equal(50m, transferEvent.Amount);
        Assert.Equal(0m, transferEvent.TotalEffect);
    }

    [Fact]
    public void GetHistoricalBalances_TransferFromExcludedIntoIncludedAccount_IsReportedAsAnIncrease()
    {
        var includedAccount = Guid.NewGuid();
        var excludedAccount = Guid.NewGuid();
        var transfer = CreateTransaction(new DateOnly(2026, 6, 2), 40m, TransactionType.Transfer, excludedAccount, includedAccount);

        var points = service.GetHistoricalBalances(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 2),
            140m,
            new[] { includedAccount },
            new[] { transfer });

        Assert.Equal(100m, points[0].Balance);
        Assert.Equal(40m, Assert.Single(points[1].Events).TotalEffect);
    }

    [Fact]
    public void GetHistoricalBalances_OrdersASingleDaysEventsByTimeThenName()
    {
        var accountId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 2);
        var untimed = CreateTransaction(date, 1m, TransactionType.Expense, accountId, name: "A untimed");
        var evening = CreateTransaction(date, 1m, TransactionType.Expense, accountId, name: "Evening", time: new TimeOnly(19, 0));
        var morning = CreateTransaction(date, 1m, TransactionType.Expense, accountId, name: "Morning", time: new TimeOnly(8, 30));

        var points = service.GetHistoricalBalances(
            date,
            date,
            100m,
            new[] { accountId },
            new[] { untimed, evening, morning });

        Assert.Equal(["Morning", "Evening", "A untimed"], points[0].Events.Select(e => e.Name));
    }

    private static Transaction CreateTransaction(
        DateOnly date,
        decimal amount,
        TransactionType type,
        Guid accountId,
        Guid? destinationAccountId = null,
        string name = "",
        TimeOnly? time = null)
    {
        return new Transaction(
            Guid.NewGuid(),
            date,
            amount,
            accountId,
            CategoryId: null,
            Notes: null,
            type,
            destinationAccountId,
            Name: name,
            Time: time);
    }
}
