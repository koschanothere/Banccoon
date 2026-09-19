namespace Banccoon.Core.Analytics;

public sealed record AnalyticsCategoryTrend(
    Guid? CategoryId,
    string CategoryName,
    IReadOnlyList<AnalyticsCategoryPoint> Points,
    decimal CurrentPeriodTotal,
    decimal PreviousPeriodTotal)
{
    public decimal ChangeAmount => CurrentPeriodTotal - PreviousPeriodTotal;

    // Null rather than an arbitrary "infinite%" when there's nothing to compare against - a
    // brand-new category (zero spend last period) isn't "up 100000%", it's just new.
    public decimal? ChangePercent => PreviousPeriodTotal == 0m
        ? null
        : ChangeAmount / PreviousPeriodTotal;
}
