using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.Core.Reconciliation;

public interface IExpectedTransactionMatcher
{
    // Already-recorded transactions that plausibly ARE this expected scheduled occurrence (e.g. the
    // imported bank row for this month's rent), best match first.
    IReadOnlyList<Transaction> FindCandidates(ForecastEvent expectedEvent, IEnumerable<Transaction> transactions);

    // Links an existing transaction to the occurrence it paid - the same PaidScheduled* link a
    // "Mark paid" transaction carries, so forecasts and "resolve upcoming" treat the occurrence as
    // paid - without creating a second transaction or touching any balance.
    Transaction Attach(Transaction transaction, ForecastEvent expectedEvent);
}
