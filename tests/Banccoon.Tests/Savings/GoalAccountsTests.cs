using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Savings;
using Xunit;

namespace Banccoon.Tests.Savings;

public sealed class GoalAccountsTests
{
    [Fact]
    public void AsSavingsGoals_TakesOnlyGoalAccounts()
    {
        var goal = CreateAccount(AccountType.Goal, 500m, target: 1000m);
        var card = CreateAccount(AccountType.DebitCard, 800m);

        var goals = GoalAccounts.AsSavingsGoals([goal, card]);

        var only = Assert.Single(goals);
        Assert.Equal(goal.Id, only.Id);
        Assert.Equal(goal.Id, only.AccountId);
        Assert.Equal(goal.Name, only.Name);
        Assert.Equal(1000m, only.TargetAmount);
        Assert.Equal(500m, only.CurrentAmount);
    }

    [Fact]
    public void FreeToSpend_ReservesTheMoneyHeldInGoalAccounts()
    {
        // Lowest forecasted balance 2000 across a card (800) and two goals (700 toward a 1000
        // target, and 900 already past its 500 target) - every unit in a goal account is held for
        // that goal, capped at its target (the allocation service's existing rule).
        var accounts = new[]
        {
            CreateAccount(AccountType.DebitCard, 800m),
            CreateAccount(AccountType.Goal, 700m, target: 1000m),
            CreateAccount(AccountType.Goal, 900m, target: 500m)
        };
        var service = new AvailableToSpendService(new SavingsGoalAllocationService());

        var breakdown = service.Calculate(CreateForecast(lowest: 2000m), GoalAccounts.AsSavingsGoals(accounts));

        Assert.Equal(1200m, breakdown.ReservedForSavingsGoals);
        Assert.Equal(800m, breakdown.AvailableToSpend);
    }

    [Fact]
    public void FreeToSpend_WithNoTarget_ReservesTheGoalAccountsWholeBalance()
    {
        var service = new AvailableToSpendService(new SavingsGoalAllocationService());

        var breakdown = service.Calculate(
            CreateForecast(lowest: 1000m),
            GoalAccounts.AsSavingsGoals([CreateAccount(AccountType.Goal, 300m, target: null)]));

        Assert.Equal(300m, breakdown.ReservedForSavingsGoals);
    }

    [Fact]
    public void FreeToSpend_NegativeGoalBalance_ReservesNothing()
    {
        var service = new AvailableToSpendService(new SavingsGoalAllocationService());

        var breakdown = service.Calculate(
            CreateForecast(lowest: 1000m),
            GoalAccounts.AsSavingsGoals([CreateAccount(AccountType.Goal, -50m, target: 400m)]));

        Assert.Equal(0m, breakdown.ReservedForSavingsGoals);
    }

    private static Account CreateAccount(AccountType type, decimal balance, decimal? target = null)
    {
        return new Account(Guid.NewGuid(), $"{type}", type, balance, "RUB", DateTimeOffset.UtcNow, PlanningValue: target);
    }

    private static ForecastResult CreateForecast(decimal lowest)
    {
        var today = new DateOnly(2026, 6, 1);
        return new ForecastResult(today, today, lowest, lowest, lowest, lowest, [], [new ProjectedBalancePoint(today, lowest)], []);
    }
}
