namespace Banccoon.Core.Savings;

public sealed record SavingsGoalAllocation(
    Guid SavingsGoalId,
    string Name,
    decimal ReservedAmount,
    decimal RemainingAmount,
    Guid? AccountId);
