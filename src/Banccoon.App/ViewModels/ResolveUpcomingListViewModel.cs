using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;

namespace Banccoon.App.ViewModels;

public sealed class ResolveUpcomingListViewModel : ViewModelBase
{
    private const int DefaultDelayDays = 7;
    private const int LookbackDays = 30;
    private const int NearTermForwardDays = 3;
    private const int ExpandedForwardDays = 90;

    private readonly IDateProvider dateProvider;
    private readonly IAccountRepository accountRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly IScheduledOccurrenceOverrideRepository scheduledOccurrenceOverrideRepository;
    private readonly IScheduledTransactionProjectionService scheduledTransactionProjectionService;
    private readonly IScheduledOccurrenceResolutionService scheduledOccurrenceResolutionService;
    private readonly ITransactionApplicationService transactionApplicationService;
    private readonly Func<Task> onChanged;

    private List<ResolveUpcomingRowViewModel> allRows = [];
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
        Func<Task> onChanged)
    {
        this.dateProvider = dateProvider;
        this.accountRepository = accountRepository;
        this.transactionRepository = transactionRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.scheduledOccurrenceOverrideRepository = scheduledOccurrenceOverrideRepository;
        this.scheduledTransactionProjectionService = scheduledTransactionProjectionService;
        this.scheduledOccurrenceResolutionService = scheduledOccurrenceResolutionService;
        this.transactionApplicationService = transactionApplicationService;
        this.onChanged = onChanged;

        Rows = [];
        ToggleExpandedCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public ObservableCollection<ResolveUpcomingRowViewModel> Rows { get; }

    public bool IsExpanded
    {
        get => isExpanded;
        private set
        {
            if (SetProperty(ref isExpanded, value))
            {
                RebuildVisibleRows();
            }
        }
    }

    public ICommand ToggleExpandedCommand { get; }

    public async Task RefreshAsync(string currency, CancellationToken cancellationToken = default)
    {
        var today = dateProvider.Today;
        nearTermCutoff = today.AddDays(NearTermForwardDays);
        var scheduledTransactions = await scheduledTransactionRepository.GetAllAsync(cancellationToken);
        var activeSchedules = scheduledTransactions.Where(schedule => schedule.Active).ToList();
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

        RebuildVisibleRows();
    }

    private void RebuildVisibleRows()
    {
        // Collapsed: near-term only (overdue, or due within NearTermForwardDays), one line per
        // distinct scheduled transaction (its most urgent occurrence). Expanded: every individual
        // occurrence across the full lookback/expanded-forward window, not just near-term ones.
        IEnumerable<ResolveUpcomingRowViewModel> visible = IsExpanded
            ? allRows
            : allRows
                .Where(row => row.OccurrenceDate <= nearTermCutoff)
                .GroupBy(row => row.ScheduledTransactionId)
                .Select(group => group.OrderBy(row => row.OccurrenceDate).First())
                .OrderBy(row => row.OccurrenceDate);

        Rows.Clear();
        foreach (var row in visible)
        {
            Rows.Add(row);
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
