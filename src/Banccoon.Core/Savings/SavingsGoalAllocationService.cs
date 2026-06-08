using Banccoon.Core.Models;

namespace Banccoon.Core.Savings;

public sealed class SavingsGoalAllocationService : ISavingsGoalAllocationService
{
    public IReadOnlyList<SavingsGoalAllocation> GetAllocations(IEnumerable<SavingsGoal> savingsGoals)
    {
        ArgumentNullException.ThrowIfNull(savingsGoals);

        return savingsGoals
            .Select(goal =>
            {
                var targetAmount = Math.Max(0m, goal.TargetAmount);
                var currentAmount = Math.Max(0m, goal.CurrentAmount);
                var reservedAmount = targetAmount > 0m
                    ? Math.Min(currentAmount, targetAmount)
                    : currentAmount;
                var remainingAmount = Math.Max(0m, targetAmount - currentAmount);

                return new SavingsGoalAllocation(
                    goal.Id,
                    goal.Name,
                    reservedAmount,
                    remainingAmount,
                    goal.AccountId);
            })
            .ToArray();
    }
}
