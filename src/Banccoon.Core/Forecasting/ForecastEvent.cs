using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public sealed record ForecastEvent(
    Guid SourceId,
    DateOnly Date,
    string Name,
    decimal Amount,
    TransactionType Type,
    Guid AccountId,
    Guid? CategoryId,
    ForecastEventKind Kind)
{
    public decimal SignedAmount => MoneyFlow.GetSignedAmount(Amount, Type);
}
