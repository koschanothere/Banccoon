using Banccoon.Core.Models;

namespace Banccoon.App.Formatting;

// How far a Goal account is toward its target (Account.PlanningValue): balance / target, clamped to
// 0..1. Shared by the Accounts row and the Dashboard's Goals widget so both show the same number.
// Null when the account isn't a goal or has no positive target to measure against.
public static class GoalProgress
{
    public static double? Of(Account account)
    {
        return account.Type == AccountType.Goal && account.PlanningValue is { } target && target > 0m
            ? (double)Math.Clamp(account.CurrentBalance / target, 0m, 1m)
            : null;
    }
}
