using Banccoon.Core.Models;

namespace Banccoon.Core.Analytics;

public sealed class AnalyticsService : IAnalyticsService
{
    private const int TopMoverCount = 5;

    public AnalyticsReport BuildReport(
        DateOnly currentPeriodAnchor,
        int trendPeriodCount,
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<Category> categories)
    {
        var periods = BuildMonthlyPeriods(currentPeriodAnchor, trendPeriodCount);
        var currentPeriod = periods[^1];
        var categoryNamesById = categories.ToDictionary(category => category.Id, category => category.Name);

        // A Transfer moves money between the user's own accounts rather than spending or earning
        // it - MoneyFlow.GetSignedAmount already treats it as zero net cash flow elsewhere in this
        // app, and analytics follows the same convention.
        var relevantTransactions = transactions
            .Where(transaction => transaction.Type != TransactionType.Transfer)
            .Where(transaction => transaction.Date >= periods[0].Start && transaction.Date <= currentPeriod.End)
            .ToList();

        var expenseTransactions = relevantTransactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .ToList();

        var categoryKeys = expenseTransactions
            .Select(transaction => transaction.CategoryId)
            .Distinct();

        var trends = categoryKeys
            .Select(categoryId => BuildTrend(categoryId, categoryNamesById, expenseTransactions, periods))
            .OrderByDescending(trend => trend.CurrentPeriodTotal)
            .ToList();

        var topMovers = trends
            .Where(trend => trend.CurrentPeriodTotal != 0m || trend.PreviousPeriodTotal != 0m)
            .OrderByDescending(trend => Math.Abs(trend.ChangeAmount))
            .Take(TopMoverCount)
            .ToList();

        var currentPeriodTransactions = relevantTransactions
            .Where(transaction => transaction.Date >= currentPeriod.Start && transaction.Date <= currentPeriod.End)
            .ToList();

        var income = currentPeriodTransactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .Sum(transaction => Math.Abs(transaction.Amount));
        var expense = currentPeriodTransactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .Sum(transaction => Math.Abs(transaction.Amount));

        return new AnalyticsReport(currentPeriod.Start, currentPeriod.End, trends, topMovers, income, expense);
    }

    private static AnalyticsCategoryTrend BuildTrend(
        Guid? categoryId,
        IReadOnlyDictionary<Guid, string> categoryNamesById,
        IReadOnlyList<Transaction> expenseTransactions,
        IReadOnlyList<MonthlyPeriod> periods)
    {
        var categoryName = categoryId is { } id && categoryNamesById.TryGetValue(id, out var name)
            ? name
            : "Uncategorized";

        var points = periods
            .Select(period => new AnalyticsCategoryPoint(
                period.Start,
                period.Label,
                expenseTransactions
                    .Where(transaction => transaction.CategoryId == categoryId
                        && transaction.Date >= period.Start
                        && transaction.Date <= period.End)
                    .Sum(transaction => Math.Abs(transaction.Amount))))
            .ToList();

        var currentTotal = points[^1].Total;
        var previousTotal = points.Count > 1 ? points[^2].Total : 0m;

        return new AnalyticsCategoryTrend(categoryId, categoryName, points, currentTotal, previousTotal);
    }

    // Steps back whole calendar months rather than a fixed day-count, since a raw
    // "AddDays(-30 * i)" walk drifts across months of different lengths (28-31 days) and would
    // stop lining up with real calendar-month boundaries after a few steps.
    private static IReadOnlyList<MonthlyPeriod> BuildMonthlyPeriods(DateOnly anchor, int count)
    {
        var periods = new List<MonthlyPeriod>(count);
        var anchorMonthStart = new DateOnly(anchor.Year, anchor.Month, 1);
        for (var i = count - 1; i >= 0; i--)
        {
            var start = anchorMonthStart.AddMonths(-i);
            var end = start.AddMonths(1).AddDays(-1);
            periods.Add(new MonthlyPeriod(start, end, start.ToString("MMM")));
        }

        return periods;
    }

    private sealed record MonthlyPeriod(DateOnly Start, DateOnly End, string Label);
}
