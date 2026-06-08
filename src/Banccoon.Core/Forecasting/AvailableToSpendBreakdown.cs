namespace Banccoon.Core.Forecasting;

public sealed record AvailableToSpendBreakdown(
    decimal LowestForecastedBalance,
    decimal ReservedForSavingsGoals,
    decimal AvailableToSpend);
