namespace Banccoon.Core.Analytics;

public sealed record AnalyticsReport(
    DateOnly CurrentPeriodStart,
    DateOnly CurrentPeriodEnd,
    IReadOnlyList<AnalyticsCategoryTrend> CategoryTrends,
    IReadOnlyList<AnalyticsCategoryTrend> TopMovers,
    decimal CurrentPeriodIncome,
    decimal CurrentPeriodExpense);
