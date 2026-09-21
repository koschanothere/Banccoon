using Banccoon.Core.Models;

namespace Banccoon.Core.Savings;

public interface ISavingsGoalAllocationService
{
    IReadOnlyList<SavingsGoalAllocation> GetAllocations(IEnumerable<SavingsGoal> savingsGoals);
}
