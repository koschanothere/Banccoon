namespace Banccoon.Core.Forecasting;

public sealed record ForecastResult(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal CurrentBalance,
    decimal ForecastedBalance,
    decimal LowestForecastedBalance,
    decimal AvailableToSpend,
    IReadOnlyList<UpcomingObligation> UpcomingObligations,
    IReadOnlyList<ProjectedBalancePoint> ProjectedBalances,
    IReadOnlyList<ForecastEvent> Events);
