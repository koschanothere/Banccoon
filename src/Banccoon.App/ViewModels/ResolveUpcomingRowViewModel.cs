using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Forecasting;

namespace Banccoon.App.ViewModels;

public sealed class ResolveUpcomingRowViewModel
{
    public ResolveUpcomingRowViewModel(
        ForecastEvent scheduledEvent,
        DateOnly today,
        string currency,
        Func<Task> onMarkPaid,
        Func<Task> onSkip,
        Func<Task> onDelay)
    {
        ScheduledTransactionId = scheduledEvent.SourceId;
        OccurrenceDate = scheduledEvent.Date;
        Name = scheduledEvent.Name;
        AmountText = MoneyFormat.Format(scheduledEvent.SignedAmount, currency);
        IsOverdue = scheduledEvent.Date < today;
        DueText = IsOverdue
            ? string.Format(Translator.Get("ResolveUpcoming_OverdueSinceFormat"), scheduledEvent.Date.ToString("dd/MM/yyyy"))
            : scheduledEvent.Date == today
                ? Translator.Get("ResolveUpcoming_DueToday")
                : string.Format(Translator.Get("ResolveUpcoming_DueFormat"), scheduledEvent.Date.ToString("dd/MM/yyyy"));

        MarkPaidCommand = new RelayCommand(() => _ = onMarkPaid());
        SkipCommand = new RelayCommand(() => _ = onSkip());
        DelayCommand = new RelayCommand(() => _ = onDelay());
    }

    public Guid ScheduledTransactionId { get; }

    public DateOnly OccurrenceDate { get; }

    public string Name { get; }

    public string AmountText { get; }

    public string DueText { get; }

    public bool IsOverdue { get; }

    public ICommand MarkPaidCommand { get; }

    public ICommand SkipCommand { get; }

    public ICommand DelayCommand { get; }
}
