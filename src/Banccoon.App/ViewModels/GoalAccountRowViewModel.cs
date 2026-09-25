using Banccoon.App.Formatting;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

// One row of the Dashboard's Goals widget: a Goal account, with the same progress/target
// presentation Accounts uses for it (see GoalProgress). Replaced the old SavingsGoalRowViewModel,
// which read the standalone SavingsGoal model the UI no longer uses.
public sealed class GoalAccountRowViewModel
{
    public GoalAccountRowViewModel(Account account)
    {
        Name = account.Name;
        CurrentText = MoneyFormat.Format(account.CurrentBalance, account.Currency);
        var progress = GoalProgress.Of(account);
        HasTarget = progress is not null;
        Progress = progress ?? 0d;
        TargetText = HasTarget
            ? MoneyFormat.Format(account.PlanningValue!.Value, account.Currency)
            : string.Empty;
    }

    public string Name { get; }

    public string CurrentText { get; }

    public bool HasTarget { get; }

    public double Progress { get; }

    public string TargetText { get; }
}
