using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class ResolveUpcomingRowViewModel : ViewModelBase
{
    private readonly Func<string, IReadOnlyList<AttachChoice>>? findAttachable;
    private readonly Func<Guid, Task>? onAttach;
    private bool isAttachOpen;
    private string attachSearchText = string.Empty;

    public ResolveUpcomingRowViewModel(
        ForecastEvent scheduledEvent,
        DateOnly today,
        string currency,
        Func<Task> onMarkPaid,
        Func<Task> onSkip,
        Func<Task> onDelay,
        Func<string, IReadOnlyList<AttachChoice>>? findAttachable = null,
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

        // "Attach…" lets the user pick the already-recorded transaction that paid this (typically
        // the imported bank row) - the occurrence is then paid without a second transaction, which
        // is what "Mark paid" would create. Nothing is suggested up front (2026-09-25).
        this.findAttachable = findAttachable;
        this.onAttach = onAttach;
        AttachChoices = [];

        MarkPaidCommand = new RelayCommand(() => _ = onMarkPaid());
        SkipCommand = new RelayCommand(() => _ = onSkip());
        DelayCommand = new RelayCommand(() => _ = onDelay());
        ToggleAttachCommand = new RelayCommand(() => IsAttachOpen = !IsAttachOpen);
    }

    public Guid ScheduledTransactionId { get; }

    public DateOnly OccurrenceDate { get; }

    public string Name { get; }

    public TransactionType Type { get; }

    public string AmountText { get; }

    public string DueText { get; }

    public bool IsOverdue { get; }

    public bool CanAttach => findAttachable is not null && onAttach is not null;

    public bool IsAttachOpen
    {
        get => isAttachOpen;
        private set
        {
            if (SetProperty(ref isAttachOpen, value))
            {
                OnPropertyChanged(nameof(AttachToggleText));
                attachSearchText = string.Empty;
                OnPropertyChanged(nameof(AttachSearchText));
                RefreshAttachChoices();
            }
        }
    }

    public string AttachToggleText => IsAttachOpen
        ? Translator.Get("Common_Cancel")
        : Translator.Get("ResolveUpcoming_AttachOpenButton");

    // Narrows the choices by name, notes or amount (IExpectedTransactionMatcher.FindAttachable).
    public string AttachSearchText
    {
        get => attachSearchText;
        set
        {
            if (SetProperty(ref attachSearchText, value))
            {
                RefreshAttachChoices();
            }
        }
    }

    public ObservableCollection<AttachChoiceViewModel> AttachChoices { get; }

    public bool HasNoAttachChoices => IsAttachOpen && AttachChoices.Count == 0;

    public ICommand MarkPaidCommand { get; }

    public ICommand SkipCommand { get; }

    public ICommand DelayCommand { get; }

    public ICommand ToggleAttachCommand { get; }

    // UI-thread only.
    private void RefreshAttachChoices()
    {
        AttachChoices.Clear();
        if (IsAttachOpen && findAttachable is not null && onAttach is not null)
        {
            foreach (var choice in findAttachable(AttachSearchText))
            {
                AttachChoices.Add(new AttachChoiceViewModel(choice, onAttach));
            }
        }

        OnPropertyChanged(nameof(HasNoAttachChoices));
    }
}

// One recorded transaction offered by "Attach…", already formatted by the list that knows the
// accounts and currency.
public sealed record AttachChoice(Guid TransactionId, string Name, string DetailText, string AmountText, TransactionType Type);

public sealed class AttachChoiceViewModel
{
    public AttachChoiceViewModel(AttachChoice choice, Func<Guid, Task> onAttach)
    {
        Name = choice.Name;
        DetailText = choice.DetailText;
        AmountText = choice.AmountText;
        Type = choice.Type;
        AttachCommand = new RelayCommand(() => _ = onAttach(choice.TransactionId));
    }

    public string Name { get; }

    public string DetailText { get; }

    public string AmountText { get; }

    public TransactionType Type { get; }

    public ICommand AttachCommand { get; }
}
