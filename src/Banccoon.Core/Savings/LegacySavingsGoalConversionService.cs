using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.Core.Savings;

// One-time move from the old standalone SavingsGoal model onto Goal accounts (the decided home for
// goals - see GoalAccounts). Additive only: the SavingsGoal rows themselves are never touched or
// deleted, and AppSettings.LegacySavingsGoalsConverted records that it has run, so a goal account
// the user later edits, archives or deletes is never recreated.
public sealed class LegacySavingsGoalConversionService : ILegacySavingsGoalConversionService
{
    private readonly ISavingsGoalRepository savingsGoalRepository;
    private readonly IAccountRepository accountRepository;
    private readonly ISettingsRepository settingsRepository;

    public LegacySavingsGoalConversionService(
        ISavingsGoalRepository savingsGoalRepository,
        IAccountRepository accountRepository,
        ISettingsRepository settingsRepository)
    {
        this.savingsGoalRepository = savingsGoalRepository;
        this.accountRepository = accountRepository;
        this.settingsRepository = settingsRepository;
    }

    public async Task<int> ConvertOnceAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken);
        if (settings.LegacySavingsGoalsConverted)
        {
            return 0;
        }

        var converted = 0;
        foreach (var goal in await savingsGoalRepository.GetAllAsync(cancellationToken))
        {
            // The goal's own id becomes the account's id, so even two conversions racing each
            // other (before the flag below is saved) can't produce two accounts for one goal.
            if (await accountRepository.GetByIdAsync(goal.Id, cancellationToken) is not null)
            {
                continue;
            }

            await accountRepository.SaveAsync(ToGoalAccount(goal, settings.DefaultCurrency), cancellationToken);
            converted++;
        }

        // Re-read right before saving, so a settings change made meanwhile isn't overwritten.
        var latest = await settingsRepository.GetAsync(cancellationToken);
        await settingsRepository.SaveAsync(latest with { LegacySavingsGoalsConverted = true }, cancellationToken);
        return converted;
    }

    // Excluded from dashboard totals: a legacy goal's CurrentAmount was a reservation inside money
    // the user's real accounts already hold (optionally the linked goal.AccountId), not separate
    // money - counting it again would inflate Current balance and the forecast by that amount. The
    // goal still shows on the Dashboard's Goals card either way, and the user can flip the flag in
    // Accounts -> Edit if the money really does sit in its own account. TargetDate has no Account
    // equivalent and stays behind in the SavingsGoal row.
    public static Account ToGoalAccount(SavingsGoal goal, string currency)
    {
        return new Account(
            goal.Id,
            goal.Name,
            AccountType.Goal,
            goal.CurrentAmount,
            currency,
            DateTimeOffset.UtcNow,
            IncludeInDashboardTotals: false,
            PlanningValue: goal.TargetAmount > 0m ? goal.TargetAmount : null);
    }
}
