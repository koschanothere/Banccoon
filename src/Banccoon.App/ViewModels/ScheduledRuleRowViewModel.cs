using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class ScheduledRuleRowViewModel
{
    public ScheduledRuleRowViewModel(
        ScheduledTransaction schedule,
        string recurrenceDescriptionText,
        string currency,
        ResolveUpcomingRowViewModel? soonestOccurrence,
        Func<Task> onEdit)
    {
        ScheduledTransactionId = schedule.Id;
        Name = schedule.Name;
        AmountText = MoneyFormat.Format(MoneyFlow.GetSignedAmount(schedule.Amount, schedule.Type), currency);
        RecurrenceDescriptionText = recurrenceDescriptionText;
        HasUpcomingOccurrence = soonestOccurrence is not null;
        NextDueText = soonestOccurrence?.DueText ?? "no upcoming occurrence";

        MarkPaidCommand = soonestOccurrence?.MarkPaidCommand ?? new RelayCommand(() => { });
        EditCommand = new RelayCommand(() => _ = onEdit());
    }

    public Guid ScheduledTransactionId { get; }

    public string Name { get; }

    public string AmountText { get; }

    public string RecurrenceDescriptionText { get; }

    public string NextDueText { get; }

    public bool HasUpcomingOccurrence { get; }

    public ICommand MarkPaidCommand { get; }

    public ICommand EditCommand { get; }
}
