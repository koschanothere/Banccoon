using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public interface IFreeToSpendWindowService
{
    FreeToSpendWindow GetWindow(
        DateOnly today,
        AppSettings settings,
        IReadOnlyCollection<ScheduledTransaction> scheduledTransactions);
}
