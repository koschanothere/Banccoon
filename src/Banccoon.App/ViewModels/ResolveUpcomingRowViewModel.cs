using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class ResolveUpcomingRowViewModel : ViewModelBase
{
    private NamedOptionViewModel? selectedAttachCandidate;

    public ResolveUpcomingRowViewModel(
        ForecastEvent scheduledEvent,
        DateOnly today,
        string currency,
        Func<Task> onMarkPaid,
        Func<Task> onSkip,
        Func<Task> onDelay,
        IReadOnlyList<NamedOptionViewModel>? attachCandidates = null,
        Func<Guid, Task>? onAttach = null)
    {
        ScheduledTransactionId = scheduledEvent.SourceId;
        OccurrenceDate = scheduledEvent.Date;
        Name = scheduledEvent.Name;
        Type = scheduledEvent.Type;
        AmountText = MoneyFormat.Format(scheduledEvent.SignedAmount, currency);
        IsOverdue = scheduledEvent.Date < today;
        DueText = IsOverdue
            ? string.Format(Translator.Get("ResolveUpcoming_OverdueSinceFormat"), scheduledEvent.Date.ToString("dd/MM/yyyy"))
            : scheduledEvent.Date == today
                ? Translator.Get("ResolveUpcoming_DueToday")
                : string.Format(Translator.Get("ResolveUpcoming_DueFormat"), scheduledEvent.Date.ToString("dd/MM/yyyy"));

        // Already-recorded transactions that look like this occurrence's real payment (typically the
        // imported bank row) - attaching one marks the occurrence paid without creating a second
        // transaction, which is what "Mark paid" would do.
        AttachCandidates = attachCandidates ?? [];
        selectedAttachCandidate = AttachCandidates.FirstOrDefault();

        MarkPaidCommand = new RelayCommand(() => _ = onMarkPaid());
        SkipCommand = new RelayCommand(() => _ = onSkip());
        DelayCommand = new RelayCommand(() => _ = onDelay());
        AttachCommand = new RelayCommand(() =>
        {
            if (onAttach is not null && SelectedAttachCandidate is { } candidate)
            {
                _ = onAttach(candidate.Id);
            }
        });
    }

    public Guid ScheduledTransactionId { get; }

    public DateOnly OccurrenceDate { get; }

    public string Name { get; }

    public TransactionType Type { get; }

    public string AmountText { get; }

    public string DueText { get; }

    public bool IsOverdue { get; }

    public IReadOnlyList<NamedOptionViewModel> AttachCandidates { get; }

    public bool HasAttachCandidates => AttachCandidates.Count > 0;

    public NamedOptionViewModel? SelectedAttachCandidate
    {
        get => selectedAttachCandidate;
        set => SetProperty(ref selectedAttachCandidate, value);
    }

    public ICommand MarkPaidCommand { get; }

    public ICommand SkipCommand { get; }

    public ICommand DelayCommand { get; }

    public ICommand AttachCommand { get; }
}
