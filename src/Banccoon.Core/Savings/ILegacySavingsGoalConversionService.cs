namespace Banccoon.Core.Savings;

public interface ILegacySavingsGoalConversionService
{
    // Converts every legacy SavingsGoal row into a Goal account, once ever (returns how many were
    // created; 0 on every call after the first).
    Task<int> ConvertOnceAsync(CancellationToken cancellationToken = default);
}
