using Banccoon.App.Formatting;
using Banccoon.Core.Appearance;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class SavingsGoalRowViewModel
{
    public SavingsGoalRowViewModel(SavingsGoal goal, string currency, DateDisplayFormat dateDisplayFormat)
    {
        Name = goal.Name;
        CurrentText = MoneyFormat.Format(goal.CurrentAmount, currency);
        TargetText = MoneyFormat.Format(goal.TargetAmount, currency);
        Progress = goal.TargetAmount > 0m
            ? (double)Math.Clamp(goal.CurrentAmount / goal.TargetAmount, 0m, 1m)
            : 0d;
        TargetDateText = goal.TargetDate is { } targetDate
            ? DateDisplay.Format(targetDate, dateDisplayFormat)
            : null;
        HasTargetDate = goal.TargetDate.HasValue;
    }

    public string Name { get; }

    public string CurrentText { get; }

    public string TargetText { get; }

    public double Progress { get; }

    public string? TargetDateText { get; }

    public bool HasTargetDate { get; }
}
