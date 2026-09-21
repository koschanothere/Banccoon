using Banccoon.Core.Analytics;
using Banccoon.Core.Models;
using Xunit;

namespace Banccoon.Tests.Analytics;

public sealed class AnalyticsServiceTests
{
    private readonly AnalyticsService service = new();

    [Fact]
    public void BuildReport_ComputesCurrentAndPreviousPeriodTotalsPerCategory()
    {
        var groceries = new Category(Guid.NewGuid(), "Groceries");
        var transactions = new List<Transaction>
        {
            Expense(new DateOnly(2026, 8, 5), 40m, groceries.Id),
            Expense(new DateOnly(2026, 9, 5), 60m, groceries.Id),
            Expense(new DateOnly(2026, 9, 20), 10m, groceries.Id)
        };

        var report = service.BuildReport(new DateOnly(2026, 9, 15), trendPeriodCount: 2, transactions, [groceries]);

        var trend = Assert.Single(report.CategoryTrends);
        Assert.Equal(groceries.Id, trend.CategoryId);
        Assert.Equal(70m, trend.CurrentPeriodTotal);
        Assert.Equal(40m, trend.PreviousPeriodTotal);
        Assert.Equal(30m, trend.ChangeAmount);
        Assert.Equal(0.75m, trend.ChangePercent);
    }

    [Fact]
    public void BuildReport_StepsBackWholeCalendarMonthsAcrossAYearBoundary()
    {
        var category = new Category(Guid.NewGuid(), "Dining");
        var transactions = new List<Transaction>
        {
            Expense(new DateOnly(2025, 12, 31), 15m, category.Id),
            Expense(new DateOnly(2026, 1, 1), 20m, category.Id)
        };

        var report = service.BuildReport(new DateOnly(2026, 1, 10), trendPeriodCount: 2, transactions, [category]);

        var trend = Assert.Single(report.CategoryTrends);
        Assert.Equal(20m, trend.CurrentPeriodTotal);
        Assert.Equal(15m, trend.PreviousPeriodTotal);
        Assert.Equal(new DateOnly(2025, 12, 1), trend.Points[0].PeriodStart);
        Assert.Equal(new DateOnly(2026, 1, 1), trend.Points[1].PeriodStart);
    }

    [Fact]
    public void BuildReport_WhenPreviousPeriodHadNoSpend_ChangePercentIsNullNotInfinite()
    {
        var category = new Category(Guid.NewGuid(), "New Hobby");
        var transactions = new List<Transaction>
        {
            Expense(new DateOnly(2026, 9, 5), 50m, category.Id)
        };

        var report = service.BuildReport(new DateOnly(2026, 9, 15), trendPeriodCount: 2, transactions, [category]);

        var trend = Assert.Single(report.CategoryTrends);
        Assert.Equal(0m, trend.PreviousPeriodTotal);
        Assert.Null(trend.ChangePercent);
    }

    [Fact]
    public void BuildReport_ExcludesTransfersFromTrendsAndIncomeExpense()
    {
        var category = new Category(Guid.NewGuid(), "Savings");
        var otherAccountId = Guid.NewGuid();
        var transactions = new List<Transaction>
        {
            new(Guid.NewGuid(), new DateOnly(2026, 9, 5), 500m, Guid.NewGuid(), category.Id, null, TransactionType.Transfer, DestinationAccountId: otherAccountId)
        };

        var report = service.BuildReport(new DateOnly(2026, 9, 15), trendPeriodCount: 1, transactions, [category]);

        Assert.Empty(report.CategoryTrends);
        Assert.Equal(0m, report.CurrentPeriodIncome);
        Assert.Equal(0m, report.CurrentPeriodExpense);
    }

    [Fact]
    public void BuildReport_ComputesIncomeAndExpenseForCurrentPeriodOnly()
    {
        var accountId = Guid.NewGuid();
        var transactions = new List<Transaction>
        {
            new(Guid.NewGuid(), new DateOnly(2026, 9, 3), 1000m, accountId, null, null, TransactionType.Income),
            new(Guid.NewGuid(), new DateOnly(2026, 9, 10), 200m, accountId, null, null, TransactionType.Expense),
            // Previous period - must not bleed into the current period's income/expense totals.
            new(Guid.NewGuid(), new DateOnly(2026, 8, 3), 900m, accountId, null, null, TransactionType.Income)
        };

        var report = service.BuildReport(new DateOnly(2026, 9, 15), trendPeriodCount: 2, transactions, []);

        Assert.Equal(1000m, report.CurrentPeriodIncome);
        Assert.Equal(200m, report.CurrentPeriodExpense);
    }

    [Fact]
    public void BuildReport_UncategorizedExpensesAreGroupedTogether()
    {
        var transactions = new List<Transaction>
        {
            Expense(new DateOnly(2026, 9, 5), 15m, categoryId: null),
            Expense(new DateOnly(2026, 9, 6), 5m, categoryId: null)
        };

        var report = service.BuildReport(new DateOnly(2026, 9, 15), trendPeriodCount: 1, transactions, []);

        var trend = Assert.Single(report.CategoryTrends);
        Assert.Null(trend.CategoryId);
        Assert.Equal("Uncategorized", trend.CategoryName);
        Assert.Equal(20m, trend.CurrentPeriodTotal);
    }

    [Fact]
    public void BuildReport_TopMoversAreOrderedByAbsoluteChangeAmount()
    {
        var bigJump = new Category(Guid.NewGuid(), "Electronics");
        var smallJump = new Category(Guid.NewGuid(), "Coffee");
        var transactions = new List<Transaction>
        {
            Expense(new DateOnly(2026, 9, 5), 500m, bigJump.Id),
            Expense(new DateOnly(2026, 9, 5), 12m, smallJump.Id),
            Expense(new DateOnly(2026, 8, 5), 10m, smallJump.Id)
        };

        var report = service.BuildReport(new DateOnly(2026, 9, 15), trendPeriodCount: 2, transactions, [bigJump, smallJump]);

        Assert.Equal(bigJump.Id, report.TopMovers[0].CategoryId);
        Assert.Equal(smallJump.Id, report.TopMovers[1].CategoryId);
    }

    private static Transaction Expense(DateOnly date, decimal amount, Guid? categoryId)
    {
        return new Transaction(Guid.NewGuid(), date, amount, Guid.NewGuid(), categoryId, null, TransactionType.Expense);
    }
}
