using Banccoon.Core.Forecasting;

namespace Banccoon.Core.Reconciliation;

public interface IReconciliationService
{
    ReconciliationResult Compare(
        ForecastResult forecastResult,
        decimal actualBalance,
        DateOnly actualBalanceDate,
        decimal tolerance = 0.01m);

    ReconciliationResult Compare(
        decimal expectedBalance,
        decimal actualBalance,
        DateOnly actualBalanceDate,
        decimal tolerance = 0.01m);
}
