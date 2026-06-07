namespace Banccoon.Core.Forecasting;

public sealed record UpcomingObligation(
    DateOnly Date,
    string Name,
    decimal Amount,
    Guid AccountId,
    Guid? CategoryId,
    ForecastEventKind Kind);
