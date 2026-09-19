namespace Banccoon.Core.Analytics;

public sealed record AnalyticsCategoryPoint(DateOnly PeriodStart, string PeriodLabel, decimal Total);
