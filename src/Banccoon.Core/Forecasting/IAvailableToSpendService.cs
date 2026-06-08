using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public interface IAvailableToSpendService
{
    AvailableToSpendBreakdown Calculate(
        ForecastResult forecast,
        IEnumerable<SavingsGoal> savingsGoals);
}
