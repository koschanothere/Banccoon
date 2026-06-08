using Banccoon.Core.Models;
using Banccoon.Core.Savings;
using Xunit;

namespace Banccoon.Tests.Savings;

public sealed class SavingsGoalAllocationServiceTests
{
    private readonly SavingsGoalAllocationService service = new();

    [Fact]
    public void GetAllocations_ReservesCurrentGoalAmount()
    {
        var goal = new SavingsGoal(
            Guid.NewGuid(),
            "Emergency fund",
            1000m,
            250m,
            new DateOnly(2026, 12, 31));

        var allocation = Assert.Single(service.GetAllocations(new[] { goal }));

        Assert.Equal(goal.Id, allocation.SavingsGoalId);
        Assert.Equal(250m, allocation.ReservedAmount);
        Assert.Equal(750m, allocation.RemainingAmount);
    }

    [Fact]
    public void GetAllocations_CapsReservedAmountAtTarget()
    {
        var goal = new SavingsGoal(
            Guid.NewGuid(),
            "Laptop",
            900m,
            1200m,
            TargetDate: null);

        var allocation = Assert.Single(service.GetAllocations(new[] { goal }));

        Assert.Equal(900m, allocation.ReservedAmount);
        Assert.Equal(0m, allocation.RemainingAmount);
    }
}
