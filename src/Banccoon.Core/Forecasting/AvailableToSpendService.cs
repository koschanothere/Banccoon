using Banccoon.Core.Models;
using Banccoon.Core.Savings;

namespace Banccoon.Core.Forecasting;

public sealed class AvailableToSpendService : IAvailableToSpendService
{
    private readonly ISavingsGoalAllocationService savingsGoalAllocationService;

    public AvailableToSpendService(ISavingsGoalAllocationService savingsGoalAllocationService)
    {
        this.savingsGoalAllocationService = savingsGoalAllocationService;
    }

    public AvailableToSpendBreakdown Calculate(
        ForecastResult forecast,
        IEnumerable<SavingsGoal> savingsGoals)
    {
        ArgumentNullException.ThrowIfNull(forecast);
        ArgumentNullException.ThrowIfNull(savingsGoals);

        var reservedForGoals = savingsGoalAllocationService
            .GetAllocations(savingsGoals)
            .Sum(allocation => allocation.ReservedAmount);

        return new AvailableToSpendBreakdown(
            forecast.LowestForecastedBalance,
            reservedForGoals,
            Math.Max(0m, forecast.LowestForecastedBalance - reservedForGoals));
    }
}
