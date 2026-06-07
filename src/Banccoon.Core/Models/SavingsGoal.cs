namespace Banccoon.Core.Models;

public sealed record SavingsGoal(
    Guid Id,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateOnly? TargetDate,
    Guid? AccountId = null);
