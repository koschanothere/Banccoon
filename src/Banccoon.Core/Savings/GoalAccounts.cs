using Banccoon.Core.Models;

namespace Banccoon.Core.Savings;

// Goals live as AccountType.Goal accounts (target in Account.PlanningValue). The older standalone
// SavingsGoal model is no longer read by the UI - its table and repository stay, per the
// additive-only migration policy. This adapts goal accounts into the SavingsGoal shape that
// SavingsGoalAllocationService / AvailableToSpendService already understand, so free-to-spend's
// "reserved for goals" reuses that tested reservation logic instead of a second copy of it.
public static class GoalAccounts
{
    // forecastAccounts: the exact accounts whose balances the forecast counted. Only money that's
    // actually inside that total can be reserved out of it - a goal account excluded from dashboard
    // totals was never counted as spendable, so there's nothing to reserve for it.
    public static IReadOnlyList<SavingsGoal> AsSavingsGoals(IEnumerable<Account> forecastAccounts)
    {
        ArgumentNullException.ThrowIfNull(forecastAccounts);

        return forecastAccounts
            .Where(account => account.Type == AccountType.Goal)
            .Select(account => new SavingsGoal(
                account.Id,
                account.Name,
                TargetAmount: account.PlanningValue ?? 0m,
                CurrentAmount: account.CurrentBalance,
                TargetDate: null,
                AccountId: account.Id))
            .ToArray();
    }
}
