using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public interface IScheduledTransactionProjectionService
{
    IReadOnlyList<ForecastEvent> Project(
        IEnumerable<ScheduledTransaction> scheduledTransactions,
        DateOnly fromInclusive,
        DateOnly toInclusive);
}
