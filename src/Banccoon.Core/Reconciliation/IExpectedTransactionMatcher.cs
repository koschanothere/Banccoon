using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.Core.Reconciliation;

public interface IExpectedTransactionMatcher
{
    // Recorded transactions the user can attach to this scheduled occurrence: every one not yet
    // linked to an occurrence, nearest the due date first, narrowed by what they typed (name, notes
    // or amount), at most `limit`. It no longer guesses by account, type and amount - that
    // suggestion was often wrong and left no way to pick anything else (user, 2026-09-25).
    IReadOnlyList<Transaction> FindAttachable(
        ForecastEvent expectedEvent,
        IEnumerable<Transaction> transactions,
        string? search,
        int limit);

    // Links an existing transaction to the occurrence it paid - the same PaidScheduled* link a
    // "Mark paid" transaction carries, so forecasts and "resolve upcoming" treat the occurrence as
    // paid - without creating a second transaction or touching any balance.
    Transaction Attach(Transaction transaction, ForecastEvent expectedEvent);
}
