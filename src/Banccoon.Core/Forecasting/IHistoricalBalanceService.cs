using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public interface IHistoricalBalanceService
{
    // currentTotalBalance is the included accounts' total at the END of endDate (i.e. after every
    // transaction dated endDate) - pass today as endDate when currentTotalBalance is the accounts'
    // live CurrentBalance sum, then drop any points you don't want to show.
    IReadOnlyList<HistoricalBalancePoint> GetHistoricalBalances(
        DateOnly startDate,
        DateOnly endDate,
        decimal currentTotalBalance,
        IReadOnlyCollection<Guid> includedAccountIds,
        IReadOnlyCollection<Transaction> transactions);
}
