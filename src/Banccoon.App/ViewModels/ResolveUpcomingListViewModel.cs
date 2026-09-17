using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;

namespace Banccoon.App.ViewModels;

public sealed class ResolveUpcomingListViewModel : ViewModelBase
{
    private const int DefaultDelayDays = 7;
    private const int LookbackDays = 30;
    private const int ExpandedForwardDays = 90;

    private readonly IDateProvider dateProvider;
    private readonly IAccountRepository accountRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly IScheduledOccurrenceOverrideRepository scheduledOccurrenceOverrideRepository;
    private readonly IScheduledTransactionProjectionService scheduledTransactionProjectionService;
    private readonly IScheduledOccurrenceResolutionService scheduledOccurrenceResolutionService;
    private readonly ITransactionApplicationService transactionApplicationService;
    private readonly IRecurrenceDescriptionService recurrenceDescriptionService;
    private readonly Func<Task> onChanged;
    private readonly Func<ScheduledTransaction, Task> onEditRequested;

    private List<ResolveUpcomingRowViewModel> allRows = [];
    private List<ScheduledTransaction> activeSchedules = [];
    private string currency = string.Empty;
    private DateOnly nearTermCutoff;
    private bool isExpanded;

    public ResolveUpcomingListViewModel(
        IDateProvider dateProvider,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        IScheduledOccurrenceOverrideRepository scheduledOccurrenceOverrideRepository,
        IScheduledTransactionProjectionService scheduledTransactionProjectionService,
        IScheduledOccurrenceResolutionService scheduledOccurrenceResolutionService,
        ITransactionApplicationService transactionApplicationService,
        IRecurrenceDescriptionService recurrenceDescriptionService,
        Func<Task> onChanged,
        Func<ScheduledTransaction, Task> onEditRequested)
    {
        this.dateProvider = dateProvider;
        this.accountRepository = accountRepository;
        this.transactionRepository = transactionRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.scheduledOccurrenceOverrideRepository = scheduledOccurrenceOverrideRepository;
        this.scheduledTransactionProjectionService = scheduledTransactionProjectionService;
        this.scheduledOccurrenceResolutionService = scheduledOccurrenceResolutionService;
        this.transactionApplicationService = transactionApplicationService;
        this.recurrenceDescriptionService = recurrenceDescriptionService;
        this.onChanged = onChanged;
        this.onEditRequested = onEditRequested;

        Rows = [];
        RuleRows = [];
        ToggleExpandedCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public ObservableCollection<ResolveUpcomingRowViewModel> Rows { get; }

    public ObservableCollection<ScheduledRuleRowViewModel> RuleRows { get; }

    public bool IsExpanded
    {
        get => isExpanded;
        private set
        {
            if (SetProperty(ref isExpanded, value))
            {
                OnPropertyChanged(nameof(IsCollapsed));
                RebuildVisibleRows();
            }
        }
    }

    public bool IsCollapsed => !IsExpanded;

    public ICommand ToggleExpandedCommand { get; }

    public async Task RefreshAsync(string currency, int nearTermDays, CancellationToken cancellationToken = default)
    {
        this.currency = currency;
        var today = dateProvider.Today;
        nearTermCutoff = today.AddDays(nearTermDays);
        var scheduledTransactions = await scheduledTransactionRepository.GetAllAsync(cancellationToken);
        activeSchedules = scheduledTransactions.Where(schedule => schedule.Active).ToList();
        // Project the full expanded window up front - collapsed view then filters down to
        // near-term (below), rather than only ever having near-term events to work with, which
        // left "expand" with nothing new to reveal beyond what was already showing.
        var projectedEvents = scheduledTransactionProjectionService.Project(
            activeSchedules, today.AddDays(-LookbackDays), today.AddDays(ExpandedForwardDays));

        var overrides = await scheduledOccurrenceOverrideRepository.GetAllAsync(cancellationToken);
        var resolvedEvents = scheduledOccurrenceResolutionService.ApplyOverrides(projectedEvents, overrides);

        var allTransactions = await transactionRepository.GetAllAsync(cancellationToken);
        var paidOccurrences = allTransactions
            .Where(transaction => transaction.PaidScheduledTransactionId.HasValue && transaction.PaidScheduledOccurrenceDate.HasValue)
            .Select(transaction => (transaction.PaidScheduledTransactionId!.Value, transaction.PaidScheduledOccurrenceDate!.Value))
            .ToHashSet();

        allRows = resolvedEvents
            .Where(scheduledEvent => !paidOccurrences.Contains((scheduledEvent.SourceId, scheduledEvent.Date)))
            .OrderBy(scheduledEvent => scheduledEvent.Date)
            .Select(scheduledEvent => new ResolveUpcomingRowViewModel(
                scheduledEvent,
                today,
                currency,
                onMarkPaid: () => MarkPaidAsync(scheduledEvent),
                onSkip: () => SkipAsync(scheduledEvent),
                onDelay: () => DelayAsync(scheduledEvent)))
            .ToList();

        // RebuildVisibleRows mutates Rows/RuleRows (bound to live CollectionViews) - must run on
        // the UI thread. Awaited-only here because RefreshAsync's own awaits above may have
        // resumed off the UI thread (see ViewModelBase.RunOnMainThreadAsync); the IsExpanded
        // setter below calls RebuildVisibleRows directly since it's already UI-thread-only.
        await RunOnMainThreadAsync(RebuildVisibleRows);
    }

    private void RebuildVisibleRows()
    {
        // Collapsed: near-term only (overdue, or due within the configured near-term window), one
        // line per distinct scheduled transaction (its most urgent occurrence).
        var nearTerm = allRows
            .Where(row => row.OccurrenceDate <= nearTermCutoff)
            .GroupBy(row => row.ScheduledTransactionId)
            .Select(group => group.OrderBy(row => row.OccurrenceDate).First())
            .OrderBy(row => row.OccurrenceDate);

        Rows.Clear();
        foreach (var row in nearTerm)
        {
            Rows.Add(row);
        }

        // Expanded: one line per rule that exists (not per occurrence), each carrying its own
        // soonest not-yet-paid occurrence (if any) for the mark-paid action, plus edit access.
        var soonestByScheduleId = allRows
            .GroupBy(row => row.ScheduledTransactionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.OccurrenceDate).First());

        RuleRows.Clear();
        foreach (var schedule in activeSchedules.OrderBy(schedule => schedule.Name))
        {
            soonestByScheduleId.TryGetValue(schedule.Id, out var soonestOccurrence);
            RuleRows.Add(new ScheduledRuleRowViewModel(
                schedule,
                recurrenceDescriptionService.Describe(schedule.RecurrenceRule),
                currency,
                soonestOccurrence,
                onEdit: () => onEditRequested(schedule)));
        }
    }

    private async Task MarkPaidAsync(ForecastEvent scheduledEvent)
    {
        var scheduledTransactions = await scheduledTransactionRepository.GetAllAsync();
        var schedule = scheduledTransactions.FirstOrDefault(schedule => schedule.Id == scheduledEvent.SourceId);
        if (schedule is null)
        {
            return;
        }

        var transaction = new Transaction(
            Guid.NewGuid(),
            scheduledEvent.Date,
            Math.Abs(schedule.Amount),
            schedule.AccountId,
            schedule.CategoryId,
            Notes: null,
            schedule.Type,
            PaidScheduledTransactionId: schedule.Id,
            PaidScheduledOccurrenceDate: scheduledEvent.Date,
            Name: schedule.Name);

        var accounts = await accountRepository.GetAllAsync();
        var accountsById = accounts.ToDictionary(account => account.Id);
        var updatedAccounts = transactionApplicationService.ApplyNewTransaction(transaction, accountsById);

        await transactionRepository.SaveAsync(transaction);
        foreach (var updatedAccount in updatedAccounts)
        {
            await accountRepository.SaveAsync(updatedAccount);
        }

        await onChanged();
    }

    private async Task SkipAsync(ForecastEvent scheduledEvent)
    {
        var occurrenceOverride = new ScheduledOccurrenceOverride(
            Guid.NewGuid(),
            scheduledEvent.SourceId,
            scheduledEvent.Date,
            ScheduledOccurrenceOverrideKind.Skipped);

        await scheduledOccurrenceOverrideRepository.SaveAsync(occurrenceOverride);
        await onChanged();
    }

    private async Task DelayAsync(ForecastEvent scheduledEvent)
    {
        var occurrenceOverride = new ScheduledOccurrenceOverride(
            Guid.NewGuid(),
            scheduledEvent.SourceId,
            scheduledEvent.Date,
            ScheduledOccurrenceOverrideKind.Delayed,
            scheduledEvent.Date.AddDays(DefaultDelayDays));

        await scheduledOccurrenceOverrideRepository.SaveAsync(occurrenceOverride);
        await onChanged();
    }
}
