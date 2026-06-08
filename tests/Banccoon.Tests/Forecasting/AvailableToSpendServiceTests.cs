using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Savings;
using Xunit;

namespace Banccoon.Tests.Forecasting;

public sealed class AvailableToSpendServiceTests
{
    private readonly AvailableToSpendService service = new(new SavingsGoalAllocationService());

    [Fact]
    public void Calculate_SubtractsSavingsReservationsFromLowestBalance()
    {
        var forecast = new ForecastResult(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            CurrentBalance: 1000m,
            ForecastedBalance: 900m,
            LowestForecastedBalance: 700m,
            AvailableToSpend: 700m,
            UpcomingObligations: Array.Empty<UpcomingObligation>(),
            ProjectedBalances: Array.Empty<ProjectedBalancePoint>(),
            Events: Array.Empty<ForecastEvent>());
        var goal = new SavingsGoal(
            Guid.NewGuid(),
            "Trip",
            1000m,
            250m,
            TargetDate: null);

        var breakdown = service.Calculate(forecast, new[] { goal });

        Assert.Equal(700m, breakdown.LowestForecastedBalance);
        Assert.Equal(250m, breakdown.ReservedForSavingsGoals);
        Assert.Equal(450m, breakdown.AvailableToSpend);
    }
}
