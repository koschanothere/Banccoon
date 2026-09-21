namespace Banccoon.Core.Analytics;

public sealed record AnalyticsCategoryTrend(
    Guid? CategoryId,
    // Null when CategoryId is null (uncategorized) - Core stays UI-language-agnostic, so the
    // "Uncategorized" display label is the App layer's job at format time, not Core's.
    string? CategoryName,
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
