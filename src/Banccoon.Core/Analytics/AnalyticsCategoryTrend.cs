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
    // Categories are two levels deep and a trend is per parent (top-level) category: its totals
    // are its own direct transactions plus every child's. For a parent that has children,
    // Breakdown splits those totals back up - one entry per child (even one with nothing spent)
    // plus, when there is any, one for the transactions filed under the parent itself
    // (IsParentOwnShare, same CategoryId as the parent). Breakdown's entries always add up to
    // this trend's points. Empty for a category with no children.
    public IReadOnlyList<AnalyticsCategoryTrend> Breakdown { get; init; } = [];

    public bool IsParentOwnShare { get; init; }

    public bool HasChildren => Breakdown.Count > 0;

    public decimal ChangeAmount => CurrentPeriodTotal - PreviousPeriodTotal;

    // Null rather than an arbitrary "infinite%" when there's nothing to compare against - a
    // brand-new category (zero spend last period) isn't "up 100000%", it's just new.
    public decimal? ChangePercent => PreviousPeriodTotal == 0m
        ? null
        : ChangeAmount / PreviousPeriodTotal;
}
