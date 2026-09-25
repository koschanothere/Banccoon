namespace Banccoon.Core.Forecasting;

// End-of-day balance for a historical day, plus the recorded transactions that happened on it -
// the historical counterpart of ProjectedBalancePoint + ForecastEvent on the forecast side.
public sealed record HistoricalBalancePoint(
    DateOnly Date,
    decimal Balance,
    IReadOnlyList<HistoricalBalanceEvent> Events);
