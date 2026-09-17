using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class ResolveUpcomingRowViewModel
{
    public ResolveUpcomingRowViewModel(ForecastEvent scheduledEvent, DateOnly today, string currency, ICommand markPaidCommand)
    {
        ScheduledTransactionId = scheduledEvent.SourceId;
        OccurrenceDate = scheduledEvent.Date;
        Name = scheduledEvent.Name;
        AmountText = MoneyFormat.Format(scheduledEvent.SignedAmount, currency);
        IsOverdue = scheduledEvent.Date < today;
        DueText = IsOverdue
            ? $"overdue since {scheduledEvent.Date:dd/MM/yyyy}"
            : scheduledEvent.Date == today
                ? "due today"
                : $"due {scheduledEvent.Date:dd/MM/yyyy}";
        MarkPaidCommand = markPaidCommand;
    }

    public Guid ScheduledTransactionId { get; }

    public DateOnly OccurrenceDate { get; }

    public string Name { get; }

    public string AmountText { get; }

    public string DueText { get; }

    public bool IsOverdue { get; }

    public ICommand MarkPaidCommand { get; }
}
