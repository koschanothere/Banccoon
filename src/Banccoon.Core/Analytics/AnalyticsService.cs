using Banccoon.Core.Categories;
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

        // Bucket every expense into its (category, period) slot in one pass instead of the
        // previous approach of re-scanning the full expense list once per category per period
        // (O(categories * periods * transactions)) - that was the dominant cost of every Dashboard
        // load, since this runs on every InitializeAsync and every month navigation.
        // Guid.Empty stands in for "uncategorized" (CategoryId is null) - safe as a dictionary key
        // sentinel since every real category Id comes from Guid.NewGuid().
        var totalsByCategory = new Dictionary<Guid, decimal[]>();
        var firstPeriodStart = periods[0].Start;
        foreach (var transaction in expenseTransactions)
        {
            var periodIndex = ((transaction.Date.Year - firstPeriodStart.Year) * 12)
                + transaction.Date.Month - firstPeriodStart.Month;
            if (periodIndex < 0 || periodIndex >= periods.Count)
            {
                continue;
            }

            var key = transaction.CategoryId ?? Guid.Empty;
            if (!totalsByCategory.TryGetValue(key, out var totals))
            {
                totals = new decimal[periods.Count];
                totalsByCategory[key] = totals;
            }

            totals[periodIndex] += Math.Abs(transaction.Amount);
        }

        // Roll each category's totals up into its parent's. A child whose parent is missing counts
        // as top-level (CategoryTree), so it still shows up, under its own name.
        var tree = new CategoryTree(categories);
        var leafKeysByRoot = totalsByCategory.Keys
            .GroupBy(key => key == Guid.Empty ? Guid.Empty : tree.RootIdOf(key))
            .ToDictionary(group => group.Key, group => group.ToList());
        var trends = leafKeysByRoot
            .Select(entry => BuildParentTrend(entry.Key, entry.Value, totalsByCategory, tree, categoryNamesById, periods))
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

    private static AnalyticsCategoryTrend BuildParentTrend(
        Guid rootKey,
        IReadOnlyList<Guid> leafKeys,
        IReadOnlyDictionary<Guid, decimal[]> totalsByCategory,
        CategoryTree tree,
        IReadOnlyDictionary<Guid, string> categoryNamesById,
        IReadOnlyList<MonthlyPeriod> periods)
    {
        var rootId = rootKey == Guid.Empty ? (Guid?)null : rootKey;
        var summed = new decimal[periods.Count];
        foreach (var key in leafKeys)
        {
            var totals = totalsByCategory[key];
            for (var index = 0; index < summed.Length; index++)
            {
                summed[index] += totals[index];
            }
        }

        var trend = BuildTrend(rootId, categoryNamesById, summed, periods);
        if (rootId is not { } parentId || !tree.HasChildren(parentId))
        {
            return trend;
        }

        var breakdown = new List<AnalyticsCategoryTrend>();
        if (totalsByCategory.TryGetValue(parentId, out var ownTotals))
        {
            breakdown.Add(BuildTrend(parentId, categoryNamesById, ownTotals, periods) with { IsParentOwnShare = true });
        }

        foreach (var child in tree.ChildrenOf(parentId))
        {
            var childTotals = totalsByCategory.TryGetValue(child.Id, out var totals) ? totals : new decimal[periods.Count];
            breakdown.Add(BuildTrend(child.Id, categoryNamesById, childTotals, periods));
        }

        return trend with { Breakdown = breakdown.OrderByDescending(entry => entry.CurrentPeriodTotal).ToList() };
    }

    private static AnalyticsCategoryTrend BuildTrend(
        Guid? categoryId,
        IReadOnlyDictionary<Guid, string> categoryNamesById,
        IReadOnlyList<decimal> periodTotals,
        IReadOnlyList<MonthlyPeriod> periods)
    {
        var categoryName = categoryId is { } id && categoryNamesById.TryGetValue(id, out var name)
            ? name
            : null;

        var points = periods
            .Select((period, index) => new AnalyticsCategoryPoint(period.Start, period.Label, periodTotals[index]))
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
