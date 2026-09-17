using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;

namespace Banccoon.App.ViewModels;

public sealed class ResolveUpcomingListViewModel : ViewModelBase
{
    private readonly IDateProvider dateProvider;
    private readonly IAccountRepository accountRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly IScheduledTransactionProjectionService scheduledTransactionProjectionService;
    private readonly ITransactionApplicationService transactionApplicationService;
    private readonly Func<Task> onChanged;

    public ResolveUpcomingListViewModel(
        IDateProvider dateProvider,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        IScheduledTransactionProjectionService scheduledTransactionProjectionService,
        ITransactionApplicationService transactionApplicationService,
        Func<Task> onChanged)
    {
        this.dateProvider = dateProvider;
        this.accountRepository = accountRepository;
        this.transactionRepository = transactionRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.scheduledTransactionProjectionService = scheduledTransactionProjectionService;
        this.transactionApplicationService = transactionApplicationService;
        this.onChanged = onChanged;

        Rows = [];
        MarkPaidCommand = new RelayCommand<Guid>(id => _ = MarkPaidAsync(id));
    }

    public ObservableCollection<ResolveUpcomingRowViewModel> Rows { get; }

    public ICommand MarkPaidCommand { get; }

    public async Task RefreshAsync(string currency, CancellationToken cancellationToken = default)
    {
        var today = dateProvider.Today;
        var scheduledTransactions = await scheduledTransactionRepository.GetAllAsync(cancellationToken);
        var activeSchedules = scheduledTransactions.Where(schedule => schedule.Active).ToList();
        var events = scheduledTransactionProjectionService.Project(activeSchedules, today.AddDays(-30), today.AddDays(3));

        var allTransactions = await transactionRepository.GetAllAsync(cancellationToken);
        var paidOccurrences = allTransactions
            .Where(transaction => transaction.PaidScheduledTransactionId.HasValue && transaction.PaidScheduledOccurrenceDate.HasValue)
            .Select(transaction => (transaction.PaidScheduledTransactionId!.Value, transaction.PaidScheduledOccurrenceDate!.Value))
            .ToHashSet();

        Rows.Clear();
        foreach (var scheduledEvent in events
                     .Where(scheduledEvent => !paidOccurrences.Contains((scheduledEvent.SourceId, scheduledEvent.Date)))
                     .OrderBy(scheduledEvent => scheduledEvent.Date))
        {
            Rows.Add(new ResolveUpcomingRowViewModel(scheduledEvent, today, currency, MarkPaidCommand));
        }
    }

    private async Task MarkPaidAsync(Guid scheduledTransactionId)
    {
        var row = Rows.FirstOrDefault(row => row.ScheduledTransactionId == scheduledTransactionId);
        if (row is null)
        {
            return;
        }

        var scheduledTransactions = await scheduledTransactionRepository.GetAllAsync();
        var schedule = scheduledTransactions.FirstOrDefault(schedule => schedule.Id == scheduledTransactionId);
        if (schedule is null)
        {
            return;
        }

        var transaction = new Core.Models.Transaction(
            Guid.NewGuid(),
            row.OccurrenceDate,
            Math.Abs(schedule.Amount),
            schedule.AccountId,
            schedule.CategoryId,
            Notes: null,
            schedule.Type,
            PaidScheduledTransactionId: schedule.Id,
            PaidScheduledOccurrenceDate: row.OccurrenceDate,
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
}
