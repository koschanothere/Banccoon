using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

// A recorded transaction that touched at least one included account on a historical day.
// TotalEffect is its signed effect on the included-accounts total (0 for a transfer between two
// included accounts, which moves money without changing the total); Amount is the transaction's
// own unsigned amount, for describing a transfer whose TotalEffect is 0.
public sealed record HistoricalBalanceEvent(
    Guid TransactionId,
    string Name,
    TransactionType Type,
    decimal Amount,
    decimal TotalEffect);
