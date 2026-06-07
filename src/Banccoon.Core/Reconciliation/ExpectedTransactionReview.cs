using Banccoon.Core.Forecasting;

namespace Banccoon.Core.Reconciliation;

public sealed record ExpectedTransactionReview(
    ForecastEvent ExpectedEvent,
    ExpectedTransactionDecision Decision = ExpectedTransactionDecision.Pending,
    DateOnly? DelayedUntil = null)
{
    public ExpectedTransactionReview Confirm()
    {
        return this with { Decision = ExpectedTransactionDecision.Confirmed, DelayedUntil = null };
    }

    public ExpectedTransactionReview DelayUntil(DateOnly delayedUntil)
    {
        return this with { Decision = ExpectedTransactionDecision.Delayed, DelayedUntil = delayedUntil };
    }

    public ExpectedTransactionReview Cancel()
    {
        return this with { Decision = ExpectedTransactionDecision.Cancelled, DelayedUntil = null };
    }
}
