using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Reconciliation;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;

namespace Banccoon.App.ViewModels;

public enum ReconciliationStep
{
    Balance,
    Expected,
    Explain,
    Adjust,
    Done
}

// The guided "check in" flow (docs/ui-structure-decisions.md, "Reconciliation"): for one account,
// compare what the bank says with what Banccoon says, then close the gap in order of how much
// real information each step keeps - resolve expected scheduled items (the same shrinking
// confirm/attach/skip/delay list as Transactions' "resolve upcoming"), record unentered spending
// as categorized transactions, and only then write off whatever is left as an explicit, auditable
// balance-adjustment transaction. The difference is recomputed after every action, so every step
// shows how much is still unexplained.
public sealed class ReconciliationViewModel : ViewModelBase
{
    private readonly IDateProvider dateProvider;
    private readonly IAccountRepository accountRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly ISettingsRepository settingsRepository;
    private readonly IReconciliationService reconciliationService;
    private readonly IBalanceAdjustmentService balanceAdjustmentService;
    private readonly ITransactionApplicationService transactionApplicationService;

    private ReconciliationStep currentStep = ReconciliationStep.Balance;
    private string currency = "EUR";
    private DateOnly today;
    private NamedOptionViewModel? selectedAccount;
    private decimal appBalance;
    private string actualBalanceText = string.Empty;
    private ReconciliationResult? comparison;
    private bool isPrefilledFromStatement;
    private bool isBusy;
    private string statusText = string.Empty;
    private string doneSummaryText = string.Empty;

    public ReconciliationViewModel(
        IDateProvider dateProvider,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        IScheduledOccurrenceOverrideRepository scheduledOccurrenceOverrideRepository,
        ISettingsRepository settingsRepository,
        IScheduledTransactionProjectionService scheduledTransactionProjectionService,
        IScheduledOccurrenceResolutionService scheduledOccurrenceResolutionService,
        ITransactionApplicationService transactionApplicationService,
        IRecurrenceDescriptionService recurrenceDescriptionService,
        IExpectedTransactionMatcher expectedTransactionMatcher,
        IReconciliationService reconciliationService,
        IGroupedSpendingService groupedSpendingService,
        IBalanceAdjustmentService balanceAdjustmentService)
    {
        this.dateProvider = dateProvider;
        this.accountRepository = accountRepository;
        this.transactionRepository = transactionRepository;
        this.settingsRepository = settingsRepository;
        this.reconciliationService = reconciliationService;
        this.balanceAdjustmentService = balanceAdjustmentService;
        this.transactionApplicationService = transactionApplicationService;

        AccountOptions = [];
        Expected = new ResolveUpcomingListViewModel(
            dateProvider,
            accountRepository,
            transactionRepository,
            scheduledTransactionRepository,
            scheduledOccurrenceOverrideRepository,
            scheduledTransactionProjectionService,
            scheduledOccurrenceResolutionService,
            transactionApplicationService,
            recurrenceDescriptionService,
            expectedTransactionMatcher,
            onChanged: RefreshAfterChangeAsync,
            onEditRequested: _ => Task.CompletedTask);
        GroupedSpending = new ReconciliationGroupedSpendingViewModel(
            categoryRepository,
            accountRepository,
            transactionRepository,
            groupedSpendingService,
            transactionApplicationService,
            onAdded: ReloadBalanceAsync);

        ContinueCommand = new RelayCommand(() => _ = ContinueAsync());
        BackCommand = new RelayCommand(GoBack);
        RecordAdjustmentCommand = new RelayCommand(() => _ = RecordAdjustmentAsync());
        FinishWithoutAdjustingCommand = new RelayCommand(FinishWithoutAdjusting);
    }

    public ResolveUpcomingListViewModel Expected { get; }

    public ReconciliationGroupedSpendingViewModel GroupedSpending { get; }

    public ObservableCollection<NamedOptionViewModel> AccountOptions { get; }

    public ReconciliationStep CurrentStep
    {
        get => currentStep;
        private set
        {
            if (SetProperty(ref currentStep, value))
            {
                OnPropertyChanged(nameof(IsBalanceStep));
                OnPropertyChanged(nameof(IsExpectedStep));
                OnPropertyChanged(nameof(IsExplainStep));
                OnPropertyChanged(nameof(IsAdjustStep));
                OnPropertyChanged(nameof(IsDoneStep));
                OnPropertyChanged(nameof(ShowsDifference));
                OnPropertyChanged(nameof(StepProgressText));
            }
        }
    }

    public bool IsBalanceStep => CurrentStep == ReconciliationStep.Balance;

    public bool IsExpectedStep => CurrentStep == ReconciliationStep.Expected;

    public bool IsExplainStep => CurrentStep == ReconciliationStep.Explain;

    public bool IsAdjustStep => CurrentStep == ReconciliationStep.Adjust;

    public bool IsDoneStep => CurrentStep == ReconciliationStep.Done;

    // The running "what's still unexplained" line shown above steps 2-4.
    public bool ShowsDifference => CurrentStep is ReconciliationStep.Expected or ReconciliationStep.Explain or ReconciliationStep.Adjust;

    public string StepProgressText => CurrentStep == ReconciliationStep.Done
        ? string.Empty
        : string.Format(Translator.Get("Reconciliation_StepProgressFormat"), (int)CurrentStep + 1, (int)ReconciliationStep.Done);

    public NamedOptionViewModel? SelectedAccount
    {
        get => selectedAccount;
        set
        {
            if (SetProperty(ref selectedAccount, value))
            {
                // Picker selection changes arrive on the UI thread.
                isPrefilledFromStatement = false;
                OnPropertyChanged(nameof(IsPrefilledFromStatement));
                _ = ReloadBalanceAsync();
            }
        }
    }

    public string ActualBalanceText
    {
        get => actualBalanceText;
        set
        {
            if (SetProperty(ref actualBalanceText, value))
            {
                UpdateComparison();
            }
        }
    }

    public bool IsPrefilledFromStatement => isPrefilledFromStatement;

    public string AppBalanceText => MoneyFormat.Format(appBalance, currency);

    public bool HasComparison => comparison is not null;

    public bool IsMatched => comparison?.Status == ReconciliationStatus.Matched;

    public bool HasDifference => comparison is not null && !IsMatched;

    public string ComparisonText => comparison is null
        ? string.Empty
        : comparison.Status switch
        {
            ReconciliationStatus.Matched => Translator.Get("Reconciliation_Matched"),
            ReconciliationStatus.Surplus => string.Format(Translator.Get("Reconciliation_SurplusFormat"), MoneyFormat.Format(Math.Abs(comparison.Difference), currency)),
            _ => string.Format(Translator.Get("Reconciliation_ShortageFormat"), MoneyFormat.Format(Math.Abs(comparison.Difference), currency))
        };

    public string AdjustmentText => comparison is null
        ? string.Empty
        : string.Format(Translator.Get("Reconciliation_AdjustmentPreviewFormat"), MoneyFormat.Format(comparison.Difference, currency));

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string DoneSummaryText
    {
        get => doneSummaryText;
        private set => SetProperty(ref doneSummaryText, value);
    }

    public ICommand ContinueCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand RecordAdjustmentCommand { get; }

    public ICommand FinishWithoutAdjustingCommand { get; }

    // fromStatementAccountId: set when arriving straight from a just-completed statement import -
    // that account is preselected, and its actual balance is prefilled with its current balance,
    // which the completed import has just set from the statement's own closing balance.
    public async Task InitializeAsync(Guid? fromStatementAccountId, CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken);
        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        var activeAccounts = accounts.Where(account => !account.IsArchived).ToList();
        var preselectedId = fromStatementAccountId ?? settings.PrimaryAccountId;
        var preselected = activeAccounts.FirstOrDefault(account => account.Id == preselectedId) ?? activeAccounts.FirstOrDefault();

        await RunOnMainThreadAsync(() =>
        {
            currency = settings.DefaultCurrency;
            today = dateProvider.Today;
            CurrentStep = ReconciliationStep.Balance;
            StatusText = string.Empty;
            DoneSummaryText = string.Empty;

            AccountOptions.Clear();
            foreach (var account in activeAccounts)
            {
                AccountOptions.Add(new NamedOptionViewModel(account.Id, account.Name));
            }

            // Set the backing field directly so the setter's "user picked a different account"
            // handling (which clears the statement prefill) doesn't run for the initial selection.
            selectedAccount = AccountOptions.FirstOrDefault(option => option.Id == preselected?.Id);
            OnPropertyChanged(nameof(SelectedAccount));

            appBalance = preselected?.CurrentBalance ?? 0m;
            isPrefilledFromStatement = fromStatementAccountId is not null && preselected?.Id == fromStatementAccountId;
            actualBalanceText = isPrefilledFromStatement
                ? appBalance.ToString("0.00", CultureInfo.InvariantCulture)
                : string.Empty;
            OnPropertyChanged(nameof(ActualBalanceText));
            OnPropertyChanged(nameof(IsPrefilledFromStatement));
            OnPropertyChanged(nameof(AppBalanceText));
            UpdateComparison();
        });
    }

    private async Task ContinueAsync()
    {
        if (IsBusy || SelectedAccount is not { } account)
        {
            return;
        }

        switch (CurrentStep)
        {
            case ReconciliationStep.Balance:
                if (comparison is null)
                {
                    StatusText = Translator.Get("Reconciliation_ActualBalanceMustBeNumber");
                    return;
                }

                await RunBusyAsync(async () =>
                {
                    await Expected.RefreshAsync(currency, nearTermDays: 0, accountId: account.Id);
                    await RunOnMainThreadAsync(() => CurrentStep = ReconciliationStep.Expected);
                });
                break;
            case ReconciliationStep.Expected:
                await RunBusyAsync(async () =>
                {
                    await GroupedSpending.LoadAsync(account.Id, currency, today);
                    await RunOnMainThreadAsync(() => CurrentStep = ReconciliationStep.Explain);
                });
                break;
            case ReconciliationStep.Explain:
                CurrentStep = ReconciliationStep.Adjust;
                break;
        }
    }

    private void GoBack()
    {
        if (IsBusy)
        {
            return;
        }

        StatusText = string.Empty;
        CurrentStep = CurrentStep switch
        {
            ReconciliationStep.Expected => ReconciliationStep.Balance,
            ReconciliationStep.Explain => ReconciliationStep.Expected,
            ReconciliationStep.Adjust => ReconciliationStep.Explain,
            _ => CurrentStep
        };
    }

    // Writes whatever difference is left off as one explicit transaction (income if the bank shows
    // more, expense if less) rather than silently overwriting the balance, so the correction stays
    // visible and auditable in Transactions.
    private async Task RecordAdjustmentAsync()
    {
        if (IsBusy || SelectedAccount is not { } account || comparison is not { } current)
        {
            return;
        }

        if (current.Status == ReconciliationStatus.Matched)
        {
            FinishWithoutAdjusting();
            return;
        }

        await RunBusyAsync(async () =>
        {
            var difference = current.Difference;
            var transaction = balanceAdjustmentService.CreateTransaction(new BalanceAdjustment(
                today,
                account.Id,
                difference,
                Translator.Get("Reconciliation_AdjustmentNote"))) with { Name = Translator.Get("Reconciliation_AdjustmentName") };

            var accounts = await accountRepository.GetAllAsync();
            var updatedAccounts = transactionApplicationService.ApplyNewTransaction(
                transaction,
                accounts.ToDictionary(accountItem => accountItem.Id));
            await transactionRepository.SaveAsync(transaction);
            foreach (var updatedAccount in updatedAccounts)
            {
                await accountRepository.SaveAsync(updatedAccount);
            }

            await ReloadBalanceAsync();
            await RunOnMainThreadAsync(() =>
            {
                DoneSummaryText = string.Format(Translator.Get("Reconciliation_DoneAdjustedFormat"), MoneyFormat.Format(difference, currency));
                CurrentStep = ReconciliationStep.Done;
            });
        });
    }

    private void FinishWithoutAdjusting()
    {
        DoneSummaryText = comparison is null || IsMatched
            ? Translator.Get("Reconciliation_DoneMatched")
            : string.Format(Translator.Get("Reconciliation_DoneUnexplainedFormat"), MoneyFormat.Format(comparison.Difference, currency));
        CurrentStep = ReconciliationStep.Done;
    }

    // Mark paid / attach / skip / delay in the expected-items step can change the account balance
    // (mark paid) and always changes which items are still expected - refresh both.
    private async Task RefreshAfterChangeAsync()
    {
        if (SelectedAccount is { } account)
        {
            await Expected.RefreshAsync(currency, nearTermDays: 0, accountId: account.Id);
        }

        await ReloadBalanceAsync();
    }

    private async Task ReloadBalanceAsync()
    {
        if (SelectedAccount is not { } account)
        {
            return;
        }

        var reloaded = await accountRepository.GetByIdAsync(account.Id);
        await RunOnMainThreadAsync(() =>
        {
            appBalance = reloaded?.CurrentBalance ?? 0m;
            OnPropertyChanged(nameof(AppBalanceText));
            UpdateComparison();
        });
    }

    // UI-thread only.
    private void UpdateComparison()
    {
        comparison = decimal.TryParse(ActualBalanceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var actual)
            ? reconciliationService.Compare(appBalance, actual, today)
            : null;

        if (comparison is not null)
        {
            StatusText = string.Empty;
        }

        OnPropertyChanged(nameof(HasComparison));
        OnPropertyChanged(nameof(IsMatched));
        OnPropertyChanged(nameof(HasDifference));
        OnPropertyChanged(nameof(ComparisonText));
        OnPropertyChanged(nameof(AdjustmentText));
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        await RunOnMainThreadAsync(() => IsBusy = true);
        try
        {
            await action();
        }
        finally
        {
            await RunOnMainThreadAsync(() => IsBusy = false);
        }
    }
}
