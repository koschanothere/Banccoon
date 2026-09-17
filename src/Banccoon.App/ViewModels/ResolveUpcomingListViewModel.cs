using System.Collections.ObjectModel;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;

namespace Banccoon.App.ViewModels;

public sealed class ResolveUpcomingListViewModel : ViewModelBase
{
    private const int DefaultDelayDays = 7;

    private readonly IDateProvider dateProvider;
    private readonly IAccountRepository accountRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly IScheduledOccurrenceOverrideRepository scheduledOccurrenceOverrideRepository;
    private readonly IScheduledTransactionProjectionService scheduledTransactionProjectionService;
    private readonly IScheduledOccurrenceResolutionService scheduledOccurrenceResolutionService;
    private readonly ITransactionApplicationService transactionApplicationService;
    private readonly Func<Task> onChanged;

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
    }

    public ObservableCollection<ResolveUpcomingRowViewModel> Rows { get; }

    public async Task RefreshAsync(string currency, CancellationToken cancellationToken = default)
    {
        var today = dateProvider.Today;
        var scheduledTransactions = await scheduledTransactionRepository.GetAllAsync(cancellationToken);
        var activeSchedules = scheduledTransactions.Where(schedule => schedule.Active).ToList();
        var projectedEvents = scheduledTransactionProjectionService.Project(activeSchedules, today.AddDays(-30), today.AddDays(3));

        var overrides = await scheduledOccurrenceOverrideRepository.GetAllAsync(cancellationToken);
        var resolvedEvents = scheduledOccurrenceResolutionService.ApplyOverrides(projectedEvents, overrides);

        var allTransactions = await transactionRepository.GetAllAsync(cancellationToken);
        var paidOccurrences = allTransactions
            .Where(transaction => transaction.PaidScheduledTransactionId.HasValue && transaction.PaidScheduledOccurrenceDate.HasValue)
            .Select(transaction => (transaction.PaidScheduledTransactionId!.Value, transaction.PaidScheduledOccurrenceDate!.Value))
            .ToHashSet();

        Rows.Clear();
        foreach (var scheduledEvent in resolvedEvents
                     .Where(scheduledEvent => !paidOccurrences.Contains((scheduledEvent.SourceId, scheduledEvent.Date)))
                     .OrderBy(scheduledEvent => scheduledEvent.Date))
        {
            var capturedEvent = scheduledEvent;
            Rows.Add(new ResolveUpcomingRowViewModel(
                capturedEvent,
                today,
                currency,
                onMarkPaid: () => MarkPaidAsync(capturedEvent),
                onSkip: () => SkipAsync(capturedEvent),
                onDelay: () => DelayAsync(capturedEvent)));
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
