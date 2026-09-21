using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public interface IHistoricalBalanceService
{
    IReadOnlyList<ProjectedBalancePoint> GetHistoricalBalances(
        DateOnly startDate,
        DateOnly endDate,
        decimal currentTotalBalance,
        IReadOnlyCollection<Guid> includedAccountIds,
        IReadOnlyCollection<Transaction> transactions);
}
