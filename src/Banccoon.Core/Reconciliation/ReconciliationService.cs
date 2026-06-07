using Banccoon.Core.Forecasting;

namespace Banccoon.Core.Reconciliation;

public sealed class ReconciliationService : IReconciliationService
{
    public ReconciliationResult Compare(
        ForecastResult forecastResult,
        decimal actualBalance,
        DateOnly actualBalanceDate,
        decimal tolerance = 0.01m)
    {
        ArgumentNullException.ThrowIfNull(forecastResult);

        var expectedBalance = forecastResult.ProjectedBalances
            .Where(point => point.Date <= actualBalanceDate)
            .OrderBy(point => point.Date)
            .LastOrDefault();

        var expected = expectedBalance is null
            ? forecastResult.CurrentBalance
            : expectedBalance.Balance;

        return Compare(expected, actualBalance, actualBalanceDate, tolerance);
    }

    public ReconciliationResult Compare(
        decimal expectedBalance,
        decimal actualBalance,
        DateOnly actualBalanceDate,
        decimal tolerance = 0.01m)
    {
        if (tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance cannot be negative.");
        }

        var difference = actualBalance - expectedBalance;
        var status = Math.Abs(difference) <= tolerance
            ? ReconciliationStatus.Matched
            : difference > 0
                ? ReconciliationStatus.Surplus
                : ReconciliationStatus.Shortage;

        return new ReconciliationResult(
            actualBalanceDate,
            expectedBalance,
            actualBalance,
            difference,
            status);
    }
}
