using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.Core.Abstractions;
using Banccoon.Core.CreditCards;
using Banccoon.Core.Forecasting;
using Banccoon.Core.ImportExport;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Reconciliation;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;
using Banccoon.Core.Statements;
using Microsoft.Maui.Storage;

namespace Banccoon.App.ViewModels;

public enum TransferDestinationKind
{
    Account,
    Goal
}

public sealed class FinanceDataViewModel : ViewModelBase
{
    private readonly IAccountRepository accountRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly ISavingsGoalRepository savingsGoalRepository;
    private readonly ISettingsRepository settingsRepository;
    private readonly IForecastService forecastService;
    private readonly ICreditCardForecastService creditCardForecastService;
    private readonly ITransactionBalanceService transactionBalanceService;
    private readonly ICheckInService checkInService;
    private readonly IReconciliationService reconciliationService;
    private readonly IGroupedSpendingService groupedSpendingService;
    private readonly IBalanceAdjustmentService balanceAdjustmentService;
    private readonly IRecurrenceService recurrenceService;
    private readonly IBackupService backupService;
    private readonly IImportService importService;
    private readonly IStatementImportService statementImportService;
    private readonly IStatementImportRepository statementImportRepository;
    private readonly ICategoryLearningRuleRepository categoryLearningRuleRepository;
    private readonly IStatementParserRegistry statementParserRegistry;
    private readonly IDateProvider dateProvider;
    private static readonly DefaultCategoryDefinition[] DefaultCategories =
    [
        new(TransactionType.Income, "Salary"),
        new(TransactionType.Income, "Freelance"),
        new(TransactionType.Income, "Refund"),
        new(TransactionType.Income, "Interest"),
        new(TransactionType.Expense, "Rent"),
        new(TransactionType.Expense, "Utilities"),
        new(TransactionType.Expense, "Groceries"),
        new(TransactionType.Expense, "Transport"),
        new(TransactionType.Expense, "Healthcare"),
        new(TransactionType.Expense, "Entertainment"),
        new(TransactionType.Expense, "Subscriptions"),
        new(TransactionType.Expense, "Card payment"),
        new(TransactionType.Expense, "Savings")
    ];

    private CancellationTokenSource? statusClearCancellation;
    private bool isLoaded;
    private bool isBusy;
    private string statusMessage = string.Empty;
    private string defaultCurrency = "EUR";
    private ForecastPeriod selectedForecastPeriod = ForecastPeriod.ThirtyDays;
    private ReminderFrequency selectedReminderFrequency = ReminderFrequency.Weekly;
    private DateDisplayFormat selectedDateDisplayFormat = DateDisplayFormat.DayMonthYear;
    private string currentBalanceText = "EUR 0.00";
    private string availableToSpendText = "EUR 0.00";
    private string lowestForecastText = "EUR 0.00";
    private string upcomingObligationsText = "EUR 0.00";
    private string dashboardCurrentBalanceText = "EUR 0.00";
    private string dashboardAvailableToSpendText = "EUR 0.00";
    private string dashboardLowestForecastText = "EUR 0.00";
    private string dashboardUpcomingObligationsText = "EUR 0.00";
    private string dashboardIncludedAccountsText = "No accounts included in dashboard totals yet.";
    private string forecastPeriodText = "30 days";
    private string newAccountName = string.Empty;
    private AccountType selectedAccountType = AccountType.DebitCard;
    private string newAccountBalanceText = "0";
    private string newAccountCurrency = "EUR";
    private string newAccountNumber = string.Empty;
    private string newCardLastFourDigits = string.Empty;
    private bool newAccountIncludeInDashboardTotals = true;
    private string newCreditCardDebtText = string.Empty;
    private string newCreditCardMinimumPaymentText = string.Empty;
    private string newCreditCardPlannedPaymentText = string.Empty;
    private string newCreditCardPaymentDueDayText = string.Empty;
    private Account? selectedAccount;
    private string selectedAccountBalanceText = string.Empty;
    private string selectedAccountDebtText = string.Empty;
    private string selectedAccountMinimumPaymentText = string.Empty;
    private string selectedAccountPlannedPaymentText = string.Empty;
    private string selectedAccountPaymentDueDayText = string.Empty;
    private string selectedAccountPayoffPaymentText = string.Empty;
    private string selectedAccountManualFinanceChargeText = "0";
    private string selectedAccountPayoffSummary = "Select a credit card to calculate payoff timing.";
    private string selectedAccountName = string.Empty;
    private AccountType selectedAccountEditType = AccountType.DebitCard;
    private string selectedAccountCurrency = "EUR";
    private string selectedAccountNumber = string.Empty;
    private string selectedCardLastFourDigits = string.Empty;
    private bool selectedAccountIncludeInDashboardTotals = true;
    private ForecastChartPointViewModel? selectedDashboardForecastPoint;
    private Account? newTransactionAccount;
    private TransferDestinationKind selectedTransferDestinationKind = TransferDestinationKind.Account;
    private Account? newTransferDestinationAccount;
    private SavingsGoal? newTransferDestinationGoal;
    private CategoryChoiceViewModel? selectedTransactionCategory;
    private string newTransactionCategoryName = string.Empty;
    private string newTransactionAmountText = string.Empty;
    private string newTransactionNotes = string.Empty;
    private DateTime newTransactionDate;
    private TransactionType selectedTransactionType = TransactionType.Expense;
    private TransactionSummaryViewModel? selectedTransactionForEditing;
    private Account? editTransactionAccount;
    private TransferDestinationKind editTransferDestinationKind = TransferDestinationKind.Account;
    private Account? editTransferDestinationAccount;
    private SavingsGoal? editTransferDestinationGoal;
    private CategoryChoiceViewModel? editTransactionCategory;
    private string editTransactionCategoryName = string.Empty;
    private string editTransactionAmountText = string.Empty;
    private string editTransactionNotes = string.Empty;
    private DateTime editTransactionDate;
    private TransactionType editTransactionType = TransactionType.Expense;
    private Account? newScheduledAccount;
    private CategoryChoiceViewModel? selectedScheduledCategory;
    private string newScheduledName = string.Empty;
    private string newScheduledAmountText = string.Empty;
    private string newScheduledNotes = string.Empty;
    private DateTime newScheduledDate;
    private TransactionType selectedScheduledType = TransactionType.Expense;
    private RecurrenceFrequency selectedScheduledFrequency = RecurrenceFrequency.Monthly;
    private string newScheduledIntervalText = "1";
    private ScheduledTransactionSummaryViewModel? selectedScheduledForEditing;
    private Account? editScheduledAccount;
    private CategoryChoiceViewModel? editScheduledCategory;
    private string editScheduledCategoryName = string.Empty;
    private string editScheduledName = string.Empty;
    private string editScheduledAmountText = string.Empty;
    private string editScheduledNotes = string.Empty;
    private DateTime editScheduledDate;
    private TransactionType editScheduledType = TransactionType.Expense;
    private RecurrenceFrequency editScheduledFrequency = RecurrenceFrequency.Monthly;
    private string editScheduledIntervalText = "1";
    private Account? newGoalAccount;
    private string newGoalName = string.Empty;
    private string newGoalTargetAmountText = string.Empty;
    private string newGoalCurrentAmountText = string.Empty;
    private DateTime newGoalTargetDate;
    private SavingsGoalSummaryViewModel? selectedGoalForEditing;
    private Account? editGoalAccount;
    private string editGoalName = string.Empty;
    private string editGoalTargetAmountText = string.Empty;
    private string editGoalCurrentAmountText = string.Empty;
    private DateTime editGoalTargetDate;
    private string newCategoryName = string.Empty;
    private DateTime checkInFromDate;
    private DateTime checkInToDate;
    private Account? reconciliationAccount;
    private string actualBalanceText = string.Empty;
    private DateTime actualBalanceDate;
    private string reconciliationExpectedText = "Not compared yet.";
    private string reconciliationActualText = "Not compared yet.";
    private string reconciliationDifferenceText = "Not compared yet.";
    private string reconciliationStatusText = "Not compared yet.";
    private ReconciliationResult? latestReconciliationResult;
    private string groupedSpendingAmountText = string.Empty;
    private string groupedSpendingNotes = string.Empty;
    private TransactionType selectedReconciliationTransactionType = TransactionType.Expense;
    private CategoryChoiceViewModel? selectedGroupedSpendingCategory;
    private string reconciliationTransactionCategoryName = string.Empty;
    private CancellationTokenSource? reconciliationComparisonCancellation;
    private string exportPathText = string.Empty;
    private string importPathText = string.Empty;
    private ImportMode selectedImportMode = ImportMode.Merge;
    private string importExportSummary = "Portable JSON export format v1.";
    private Account? statementImportAccount;
    private string statementImportPathText = string.Empty;
    private string statementImportStatusText = "No statement selected.";
    private ParsedStatement? pendingStatementPreview;
    private string pendingStatementFilePath = string.Empty;
    private Account? statementConnectAccount;
    private string statementNewAccountName = "Imported statement account";
    private string statementNewAccountCurrency = "RUB";
    private string statementSelectedFileText = "No file selected.";
    private string statementDetectedAccountText = "No statement account detected yet.";
    private string statementDetectedCardText = string.Empty;
    private string statementMatchedAccountText = "No account matched yet.";
    private bool isStatementAccountUnmatched;
    private StatementImportBatchSummaryViewModel? selectedStatementImportBatch;
    private StatementImportRowSummaryViewModel? selectedStatementImportRow;
    private CategoryChoiceViewModel? selectedStatementImportCategory;
    private string statementImportCategoryName = string.Empty;
    private string deleteAllConfirmationText = string.Empty;
    private bool deleteAllBackupAcknowledged;
    private bool isAccountsEditing;
    private bool isTransactionsEditing;
    private bool isScheduledEditing;
    private bool isGoalsEditing;

    public FinanceDataViewModel(
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        ISavingsGoalRepository savingsGoalRepository,
        ISettingsRepository settingsRepository,
        IForecastService forecastService,
        ICreditCardForecastService creditCardForecastService,
        ITransactionBalanceService transactionBalanceService,
        ICheckInService checkInService,
        IReconciliationService reconciliationService,
        IGroupedSpendingService groupedSpendingService,
        IBalanceAdjustmentService balanceAdjustmentService,
        IRecurrenceService recurrenceService,
        IBackupService backupService,
        IImportService importService,
        IStatementImportService statementImportService,
        IStatementImportRepository statementImportRepository,
        ICategoryLearningRuleRepository categoryLearningRuleRepository,
        IStatementParserRegistry statementParserRegistry,
        IDateProvider dateProvider)
    {
        this.accountRepository = accountRepository;
        this.categoryRepository = categoryRepository;
        this.transactionRepository = transactionRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.savingsGoalRepository = savingsGoalRepository;
        this.settingsRepository = settingsRepository;
        this.forecastService = forecastService;
        this.creditCardForecastService = creditCardForecastService;
        this.transactionBalanceService = transactionBalanceService;
        this.checkInService = checkInService;
        this.reconciliationService = reconciliationService;
        this.groupedSpendingService = groupedSpendingService;
        this.balanceAdjustmentService = balanceAdjustmentService;
        this.recurrenceService = recurrenceService;
        this.backupService = backupService;
        this.importService = importService;
        this.statementImportService = statementImportService;
        this.statementImportRepository = statementImportRepository;
        this.categoryLearningRuleRepository = categoryLearningRuleRepository;
        this.statementParserRegistry = statementParserRegistry;
        this.dateProvider = dateProvider;

        var today = dateProvider.Today.ToDateTime(TimeOnly.MinValue);
        newTransactionDate = today;
        editTransactionDate = today;
        newScheduledDate = today;
        editScheduledDate = today;
        newGoalTargetDate = today.AddMonths(6);
        editGoalTargetDate = today.AddMonths(6);
        checkInFromDate = today.AddDays(-7);
        checkInToDate = today;
        actualBalanceDate = today;
        exportPathText = CreateDefaultBackupPath();

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AddAccountCommand = new AsyncRelayCommand(AddAccountAsync);
        SaveSelectedAccountCommand = new AsyncRelayCommand(SaveSelectedAccountAsync);
        DeleteAccountCommand = new AsyncRelayCommand<Account>(DeleteAccountAsync);
        AddTransactionCommand = new AsyncRelayCommand(AddTransactionAsync);
        SaveEditedTransactionCommand = new AsyncRelayCommand(SaveEditedTransactionAsync);
        DeleteTransactionCommand = new AsyncRelayCommand<Transaction>(DeleteTransactionAsync);
        AddCategoryCommand = new AsyncRelayCommand(AddCategoryAsync);
        DeleteCategoryCommand = new AsyncRelayCommand<Category>(DeleteCategoryAsync);
        AddScheduledTransactionCommand = new AsyncRelayCommand(AddScheduledTransactionAsync);
        SaveEditedScheduledTransactionCommand = new AsyncRelayCommand(SaveEditedScheduledTransactionAsync);
        DeleteScheduledTransactionCommand = new AsyncRelayCommand<ScheduledTransaction>(DeleteScheduledTransactionAsync);
        AddSavingsGoalCommand = new AsyncRelayCommand(AddSavingsGoalAsync);
        SaveEditedSavingsGoalCommand = new AsyncRelayCommand(SaveEditedSavingsGoalAsync);
        DeleteSavingsGoalCommand = new AsyncRelayCommand<SavingsGoal>(DeleteSavingsGoalAsync);
        CreateCheckInCommand = new AsyncRelayCommand(CreateCheckInAsync);
        ConfirmExpectedTransactionCommand = new AsyncRelayCommand<ExpectedTransactionReviewViewModel>(ConfirmExpectedTransactionAsync);
        DelayExpectedTransactionCommand = new AsyncRelayCommand<ExpectedTransactionReviewViewModel>(DelayExpectedTransactionAsync);
        CancelExpectedTransactionCommand = new AsyncRelayCommand<ExpectedTransactionReviewViewModel>(CancelExpectedTransactionAsync);
        CompareRealityCommand = new AsyncRelayCommand(CompareRealityAsync);
        AddGroupedSpendingCommand = new AsyncRelayCommand(AddGroupedSpendingAsync);
        AddBalanceAdjustmentCommand = new AsyncRelayCommand(AddBalanceAdjustmentAsync);
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
        ValidateImportCommand = new AsyncRelayCommand(ValidateImportAsync);
        RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync);
        PickStatementFileCommand = new AsyncRelayCommand(PickStatementFileAsync);
        AnalyzeStatementCommand = new AsyncRelayCommand(AnalyzeStatementAsync);
        ConnectStatementAccountCommand = new AsyncRelayCommand(ConnectStatementAccountAsync);
        CreateStatementAccountFromStatementCommand = new AsyncRelayCommand(CreateStatementAccountFromStatementAsync);
        CancelStatementImportCommand = new AsyncRelayCommand(CancelStatementImportAsync);
        RefreshStatementImportsCommand = new AsyncRelayCommand(RefreshStatementImportsAsync);
        ApproveStatementRowCommand = new AsyncRelayCommand<StatementImportRowSummaryViewModel>(ApproveStatementRowAsync);
        SkipStatementRowCommand = new AsyncRelayCommand<StatementImportRowSummaryViewModel>(SkipStatementRowAsync);
        ResetExportPathCommand = new RelayCommand(() => ExportPathText = CreateDefaultBackupPath());
        CopyExportPathToImportCommand = new RelayCommand(() => ImportPathText = ExportPathText);
        DeleteAllDataCommand = new AsyncRelayCommand(DeleteAllDataAsync);
        SavePreferencesCommand = new AsyncRelayCommand(SavePreferencesAsync);
        ToggleAccountsEditingCommand = new RelayCommand(() => IsAccountsEditing = !IsAccountsEditing);
        ToggleTransactionsEditingCommand = new RelayCommand(() => IsTransactionsEditing = !IsTransactionsEditing);
        ToggleScheduledEditingCommand = new RelayCommand(() => IsScheduledEditing = !IsScheduledEditing);
        ToggleGoalsEditingCommand = new RelayCommand(() => IsGoalsEditing = !IsGoalsEditing);
    }

    public ObservableCollection<Account> Accounts { get; } = new();

    public ObservableCollection<AccountSummaryViewModel> AccountSummaries { get; } = new();

    public ObservableCollection<Category> Categories { get; } = new();

    public ObservableCollection<CategoryChoiceViewModel> TransactionCategoryChoices { get; } = new();

    public ObservableCollection<CategoryChoiceViewModel> EditTransactionCategoryChoices { get; } = new();

    public ObservableCollection<CategoryChoiceViewModel> ScheduledCategoryChoices { get; } = new();

    public ObservableCollection<CategoryChoiceViewModel> EditScheduledCategoryChoices { get; } = new();

    public ObservableCollection<CategoryChoiceViewModel> GroupedSpendingCategoryChoices { get; } = new();

    public ObservableCollection<CategorySummaryViewModel> CategorySummaries { get; } = new();

    public ObservableCollection<Transaction> Transactions { get; } = new();

    public ObservableCollection<TransactionSummaryViewModel> TransactionSummaries { get; } = new();

    public ObservableCollection<ScheduledTransaction> ScheduledTransactions { get; } = new();

    public ObservableCollection<ScheduledTransactionSummaryViewModel> ScheduledTransactionSummaries { get; } = new();

    public ObservableCollection<SavingsGoal> SavingsGoals { get; } = new();

    public ObservableCollection<SavingsGoalSummaryViewModel> SavingsGoalSummaries { get; } = new();

    public ObservableCollection<ForecastEventSummaryViewModel> ForecastEvents { get; } = new();

    public ObservableCollection<UpcomingObligationSummaryViewModel> UpcomingObligations { get; } = new();

    public ObservableCollection<ForecastChartPointViewModel> DashboardForecastPoints { get; } = new();

    public ObservableCollection<StatementImportBatchSummaryViewModel> StatementImportBatchSummaries { get; } = new();

    public ObservableCollection<StatementImportRowSummaryViewModel> StatementImportRows { get; } = new();

    public ObservableCollection<CategoryChoiceViewModel> StatementImportCategoryChoices { get; } = new();

    public ObservableCollection<ExpectedTransactionReviewViewModel> CheckInExpectedTransactions { get; } = new();

    public IReadOnlyList<AccountType> AccountTypes { get; } = Enum.GetValues<AccountType>();

    public IReadOnlyList<TransactionType> TransactionTypes { get; } = Enum.GetValues<TransactionType>();

    public IReadOnlyList<TransactionType> ReconciliationTransactionTypes { get; } =
    [
        TransactionType.Expense,
        TransactionType.Income
    ];

    public IReadOnlyList<TransferDestinationKind> TransferDestinationKinds { get; } = Enum.GetValues<TransferDestinationKind>();

    public IReadOnlyList<RecurrenceFrequency> RecurrenceFrequencies { get; } = Enum.GetValues<RecurrenceFrequency>();

    public IReadOnlyList<ForecastPeriod> ForecastPeriods { get; } = Enum.GetValues<ForecastPeriod>();

    public IReadOnlyList<ReminderFrequency> ReminderFrequencies { get; } = Enum.GetValues<ReminderFrequency>();

    public IReadOnlyList<DateDisplayFormat> DateDisplayFormats { get; } = Enum.GetValues<DateDisplayFormat>();

    public IReadOnlyList<ImportMode> ImportModes { get; } =
    [
        ImportMode.Merge,
        ImportMode.Replace,
        ImportMode.ValidateOnly
    ];

    public IReadOnlyList<string> SupportedCurrencies { get; } =
    [
        "EUR",
        "USD",
        "GBP",
        "PLN",
        "CZK",
        "CHF",
        "NOK",
        "SEK",
        "DKK",
        "JPY",
        "CAD",
        "AUD",
        "RUB"
    ];

    public ICommand LoadCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand AddAccountCommand { get; }

    public ICommand SaveSelectedAccountCommand { get; }

    public ICommand DeleteAccountCommand { get; }

    public ICommand AddTransactionCommand { get; }

    public ICommand SaveEditedTransactionCommand { get; }

    public ICommand DeleteTransactionCommand { get; }

    public ICommand AddCategoryCommand { get; }

    public ICommand DeleteCategoryCommand { get; }

    public ICommand AddScheduledTransactionCommand { get; }

    public ICommand SaveEditedScheduledTransactionCommand { get; }

    public ICommand DeleteScheduledTransactionCommand { get; }

    public ICommand AddSavingsGoalCommand { get; }

    public ICommand SaveEditedSavingsGoalCommand { get; }

    public ICommand DeleteSavingsGoalCommand { get; }

    public ICommand CreateCheckInCommand { get; }

    public ICommand ConfirmExpectedTransactionCommand { get; }

    public ICommand DelayExpectedTransactionCommand { get; }

    public ICommand CancelExpectedTransactionCommand { get; }

    public ICommand CompareRealityCommand { get; }

    public ICommand AddGroupedSpendingCommand { get; }

    public ICommand AddBalanceAdjustmentCommand { get; }

    public ICommand CreateBackupCommand { get; }

    public ICommand ValidateImportCommand { get; }

    public ICommand RestoreBackupCommand { get; }

    public ICommand PickStatementFileCommand { get; }

    public ICommand AnalyzeStatementCommand { get; }

    public ICommand ConnectStatementAccountCommand { get; }

    public ICommand CreateStatementAccountFromStatementCommand { get; }

    public ICommand CancelStatementImportCommand { get; }

    public ICommand RefreshStatementImportsCommand { get; }

    public ICommand ApproveStatementRowCommand { get; }

    public ICommand SkipStatementRowCommand { get; }

    public ICommand ResetExportPathCommand { get; }

    public ICommand CopyExportPathToImportCommand { get; }

    public ICommand DeleteAllDataCommand { get; }

    public ICommand SavePreferencesCommand { get; }

    public ICommand ToggleAccountsEditingCommand { get; }

    public ICommand ToggleTransactionsEditingCommand { get; }

    public ICommand ToggleScheduledEditingCommand { get; }

    public ICommand ToggleGoalsEditingCommand { get; }

    public bool IsAccountsEditing
    {
        get => isAccountsEditing;
        set
        {
            if (SetProperty(ref isAccountsEditing, value))
            {
                OnPropertyChanged(nameof(AccountsEditModeText));
            }
        }
    }

    public bool IsTransactionsEditing
    {
        get => isTransactionsEditing;
        set
        {
            if (SetProperty(ref isTransactionsEditing, value))
            {
                OnPropertyChanged(nameof(TransactionsEditModeText));
            }
        }
    }

    public bool IsScheduledEditing
    {
        get => isScheduledEditing;
        set
        {
            if (SetProperty(ref isScheduledEditing, value))
            {
                OnPropertyChanged(nameof(ScheduledEditModeText));
            }
        }
    }

    public bool IsGoalsEditing
    {
        get => isGoalsEditing;
        set
        {
            if (SetProperty(ref isGoalsEditing, value))
            {
                OnPropertyChanged(nameof(GoalsEditModeText));
            }
        }
    }

    public string AccountsEditModeText => IsAccountsEditing ? "Done" : "✎";

    public string TransactionsEditModeText => IsTransactionsEditing ? "Done" : "✎";

    public string ScheduledEditModeText => IsScheduledEditing ? "Done" : "✎";

    public string GoalsEditModeText => IsGoalsEditing ? "Done" : "✎";

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            if (SetProperty(ref statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public string DefaultCurrency
    {
        get => defaultCurrency;
        set
        {
            if (SetProperty(ref defaultCurrency, NormalizeCurrency(value)))
            {
                if (string.IsNullOrWhiteSpace(NewAccountCurrency))
                {
                    NewAccountCurrency = defaultCurrency;
                }
            }
        }
    }

    public ForecastPeriod SelectedForecastPeriod
    {
        get => selectedForecastPeriod;
        set
        {
            if (SetProperty(ref selectedForecastPeriod, value))
            {
                UpdateForecast();
            }
        }
    }

    public ReminderFrequency SelectedReminderFrequency
    {
        get => selectedReminderFrequency;
        set => SetProperty(ref selectedReminderFrequency, value);
    }

    public DateDisplayFormat SelectedDateDisplayFormat
    {
        get => selectedDateDisplayFormat;
        set
        {
            if (SetProperty(ref selectedDateDisplayFormat, value))
            {
                UpdateSummaries();
                UpdateForecast();
                OnPropertyChanged(nameof(DatePickerFormat));
                OnPropertyChanged(nameof(ScheduledRecurrenceSentencePreview));
            }
        }
    }

    public string DatePickerFormat => DateDisplay.GetPattern(SelectedDateDisplayFormat);

    public string CurrentBalanceText
    {
        get => currentBalanceText;
        private set => SetProperty(ref currentBalanceText, value);
    }

    public string AvailableToSpendText
    {
        get => availableToSpendText;
        private set => SetProperty(ref availableToSpendText, value);
    }

    public string LowestForecastText
    {
        get => lowestForecastText;
        private set => SetProperty(ref lowestForecastText, value);
    }

    public string UpcomingObligationsText
    {
        get => upcomingObligationsText;
        private set => SetProperty(ref upcomingObligationsText, value);
    }

    public string DashboardCurrentBalanceText
    {
        get => dashboardCurrentBalanceText;
        private set => SetProperty(ref dashboardCurrentBalanceText, value);
    }

    public string DashboardAvailableToSpendText
    {
        get => dashboardAvailableToSpendText;
        private set => SetProperty(ref dashboardAvailableToSpendText, value);
    }

    public string DashboardLowestForecastText
    {
        get => dashboardLowestForecastText;
        private set => SetProperty(ref dashboardLowestForecastText, value);
    }

    public string DashboardUpcomingObligationsText
    {
        get => dashboardUpcomingObligationsText;
        private set => SetProperty(ref dashboardUpcomingObligationsText, value);
    }

    public string DashboardIncludedAccountsText
    {
        get => dashboardIncludedAccountsText;
        private set => SetProperty(ref dashboardIncludedAccountsText, value);
    }

    public string ForecastPeriodText
    {
        get => forecastPeriodText;
        private set => SetProperty(ref forecastPeriodText, value);
    }

    public bool HasAccounts => Accounts.Count > 0;

    public bool HasCategories => Categories.Count > 0;

    public bool HasTransactions => Transactions.Count > 0;

    public bool HasScheduledTransactions => ScheduledTransactions.Count > 0;

    public bool HasSavingsGoals => SavingsGoals.Count > 0;

    public bool HasForecastEvents => ForecastEvents.Count > 0;

    public bool HasUpcomingObligations => UpcomingObligations.Count > 0;

    public bool HasDashboardForecastPoints => DashboardForecastPoints.Count > 0;

    public ForecastChartPointViewModel? SelectedDashboardForecastPoint
    {
        get => selectedDashboardForecastPoint;
        set
        {
            if (SetProperty(ref selectedDashboardForecastPoint, value))
            {
                OnPropertyChanged(nameof(HasSelectedDashboardForecastPoint));
            }
        }
    }

    public bool HasSelectedDashboardForecastPoint => SelectedDashboardForecastPoint is not null;

    public string NewAccountName
    {
        get => newAccountName;
        set => SetProperty(ref newAccountName, value);
    }

    public AccountType SelectedAccountType
    {
        get => selectedAccountType;
        set
        {
            if (SetProperty(ref selectedAccountType, value))
            {
                OnPropertyChanged(nameof(IsNewAccountCreditCard));
                OnPropertyChanged(nameof(IsNewAccountCard));
                NewAccountIncludeInDashboardTotals = value != AccountType.CreditCard;
            }
        }
    }

    public bool IsNewAccountCreditCard => SelectedAccountType == AccountType.CreditCard;

    public bool IsNewAccountCard => SelectedAccountType is AccountType.DebitCard or AccountType.CreditCard;

    public string NewAccountBalanceText
    {
        get => newAccountBalanceText;
        set => SetProperty(ref newAccountBalanceText, value);
    }

    public string NewAccountCurrency
    {
        get => newAccountCurrency;
        set => SetProperty(ref newAccountCurrency, NormalizeCurrency(value));
    }

    public string NewAccountNumber
    {
        get => newAccountNumber;
        set => SetProperty(ref newAccountNumber, value);
    }

    public string NewCardLastFourDigits
    {
        get => newCardLastFourDigits;
        set => SetProperty(ref newCardLastFourDigits, value);
    }

    public bool NewAccountIncludeInDashboardTotals
    {
        get => newAccountIncludeInDashboardTotals;
        set => SetProperty(ref newAccountIncludeInDashboardTotals, value);
    }

    public string NewCreditCardDebtText
    {
        get => newCreditCardDebtText;
        set => SetProperty(ref newCreditCardDebtText, value);
    }

    public string NewCreditCardMinimumPaymentText
    {
        get => newCreditCardMinimumPaymentText;
        set => SetProperty(ref newCreditCardMinimumPaymentText, value);
    }

    public string NewCreditCardPlannedPaymentText
    {
        get => newCreditCardPlannedPaymentText;
        set => SetProperty(ref newCreditCardPlannedPaymentText, value);
    }

    public string NewCreditCardPaymentDueDayText
    {
        get => newCreditCardPaymentDueDayText;
        set => SetProperty(ref newCreditCardPaymentDueDayText, value);
    }

    public Account? SelectedAccount
    {
        get => selectedAccount;
        set
        {
            if (SetProperty(ref selectedAccount, value))
            {
                LoadSelectedAccountEditor(value);
                OnPropertyChanged(nameof(IsSelectedAccountCreditCard));
                OnPropertyChanged(nameof(IsSelectedAccountEditCreditCard));
                OnPropertyChanged(nameof(IsSelectedAccountEditCard));
                RecalculateSelectedAccountPayoff();
            }
        }
    }

    public bool IsSelectedAccountCreditCard => SelectedAccount?.Type == AccountType.CreditCard;

    public string SelectedAccountName
    {
        get => selectedAccountName;
        set => SetProperty(ref selectedAccountName, value);
    }

    public AccountType SelectedAccountEditType
    {
        get => selectedAccountEditType;
        set
        {
            if (SetProperty(ref selectedAccountEditType, value))
            {
                OnPropertyChanged(nameof(IsSelectedAccountEditCreditCard));
                OnPropertyChanged(nameof(IsSelectedAccountEditCard));
            }
        }
    }

    public bool IsSelectedAccountEditCreditCard => SelectedAccountEditType == AccountType.CreditCard;

    public bool IsSelectedAccountEditCard => SelectedAccountEditType is AccountType.DebitCard or AccountType.CreditCard;

    public string SelectedAccountCurrency
    {
        get => selectedAccountCurrency;
        set => SetProperty(ref selectedAccountCurrency, NormalizeCurrency(value));
    }

    public string SelectedAccountNumber
    {
        get => selectedAccountNumber;
        set => SetProperty(ref selectedAccountNumber, value);
    }

    public string SelectedCardLastFourDigits
    {
        get => selectedCardLastFourDigits;
        set => SetProperty(ref selectedCardLastFourDigits, value);
    }

    public bool SelectedAccountIncludeInDashboardTotals
    {
        get => selectedAccountIncludeInDashboardTotals;
        set => SetProperty(ref selectedAccountIncludeInDashboardTotals, value);
    }

    public string SelectedAccountBalanceText
    {
        get => selectedAccountBalanceText;
        set => SetProperty(ref selectedAccountBalanceText, value);
    }

    public string SelectedAccountDebtText
    {
        get => selectedAccountDebtText;
        set
        {
            if (SetProperty(ref selectedAccountDebtText, value))
            {
                RecalculateSelectedAccountPayoff();
            }
        }
    }

    public string SelectedAccountMinimumPaymentText
    {
        get => selectedAccountMinimumPaymentText;
        set
        {
            if (SetProperty(ref selectedAccountMinimumPaymentText, value))
            {
                RecalculateSelectedAccountPayoff();
            }
        }
    }

    public string SelectedAccountPlannedPaymentText
    {
        get => selectedAccountPlannedPaymentText;
        set
        {
            if (SetProperty(ref selectedAccountPlannedPaymentText, value))
            {
                RecalculateSelectedAccountPayoff();
            }
        }
    }

    public string SelectedAccountPaymentDueDayText
    {
        get => selectedAccountPaymentDueDayText;
        set => SetProperty(ref selectedAccountPaymentDueDayText, value);
    }

    public string SelectedAccountPayoffPaymentText
    {
        get => selectedAccountPayoffPaymentText;
        set
        {
            if (SetProperty(ref selectedAccountPayoffPaymentText, value))
            {
                RecalculateSelectedAccountPayoff();
            }
        }
    }

    public string SelectedAccountManualFinanceChargeText
    {
        get => selectedAccountManualFinanceChargeText;
        set
        {
            if (SetProperty(ref selectedAccountManualFinanceChargeText, value))
            {
                RecalculateSelectedAccountPayoff();
            }
        }
    }

    public string SelectedAccountPayoffSummary
    {
        get => selectedAccountPayoffSummary;
        private set => SetProperty(ref selectedAccountPayoffSummary, value);
    }

    public Account? NewTransactionAccount
    {
        get => newTransactionAccount;
        set => SetProperty(ref newTransactionAccount, value);
    }

    public TransferDestinationKind SelectedTransferDestinationKind
    {
        get => selectedTransferDestinationKind;
        set
        {
            if (SetProperty(ref selectedTransferDestinationKind, value))
            {
                OnPropertyChanged(nameof(IsNewTransferToAccount));
                OnPropertyChanged(nameof(IsNewTransferToGoal));
            }
        }
    }

    public bool IsNewTransferToAccount => IsNewTransactionTransfer && SelectedTransferDestinationKind == TransferDestinationKind.Account;

    public bool IsNewTransferToGoal => IsNewTransactionTransfer && SelectedTransferDestinationKind == TransferDestinationKind.Goal;

    public Account? NewTransferDestinationAccount
    {
        get => newTransferDestinationAccount;
        set => SetProperty(ref newTransferDestinationAccount, value);
    }

    public SavingsGoal? NewTransferDestinationGoal
    {
        get => newTransferDestinationGoal;
        set => SetProperty(ref newTransferDestinationGoal, value);
    }

    public CategoryChoiceViewModel? SelectedTransactionCategory
    {
        get => selectedTransactionCategory;
        set => SetProperty(ref selectedTransactionCategory, value);
    }

    public string NewTransactionCategoryName
    {
        get => newTransactionCategoryName;
        set => SetProperty(ref newTransactionCategoryName, value);
    }

    public string NewTransactionAmountText
    {
        get => newTransactionAmountText;
        set => SetProperty(ref newTransactionAmountText, value);
    }

    public string NewTransactionNotes
    {
        get => newTransactionNotes;
        set => SetProperty(ref newTransactionNotes, value);
    }

    public DateTime NewTransactionDate
    {
        get => newTransactionDate;
        set => SetProperty(ref newTransactionDate, value);
    }

    public TransactionType SelectedTransactionType
    {
        get => selectedTransactionType;
        set
        {
            if (SetProperty(ref selectedTransactionType, value))
            {
                UpdateCategoryChoices();
                SelectedTransactionCategory = TransactionCategoryChoices.FirstOrDefault();
                OnPropertyChanged(nameof(IsNewTransactionTransfer));
                OnPropertyChanged(nameof(IsNewTransactionNotTransfer));
                OnPropertyChanged(nameof(IsNewTransferToAccount));
                OnPropertyChanged(nameof(IsNewTransferToGoal));
            }
        }
    }

    public bool IsNewTransactionTransfer => SelectedTransactionType == TransactionType.Transfer;

    public bool IsNewTransactionNotTransfer => !IsNewTransactionTransfer;

    public TransactionSummaryViewModel? SelectedTransactionForEditing
    {
        get => selectedTransactionForEditing;
        set
        {
            if (SetProperty(ref selectedTransactionForEditing, value))
            {
                LoadTransactionEditor(value?.Source);
            }
        }
    }

    public Account? EditTransactionAccount
    {
        get => editTransactionAccount;
        set => SetProperty(ref editTransactionAccount, value);
    }

    public TransferDestinationKind EditTransferDestinationKind
    {
        get => editTransferDestinationKind;
        set
        {
            if (SetProperty(ref editTransferDestinationKind, value))
            {
                OnPropertyChanged(nameof(IsEditTransferToAccount));
                OnPropertyChanged(nameof(IsEditTransferToGoal));
            }
        }
    }

    public bool IsEditTransferToAccount => IsEditTransactionTransfer && EditTransferDestinationKind == TransferDestinationKind.Account;

    public bool IsEditTransferToGoal => IsEditTransactionTransfer && EditTransferDestinationKind == TransferDestinationKind.Goal;

    public Account? EditTransferDestinationAccount
    {
        get => editTransferDestinationAccount;
        set => SetProperty(ref editTransferDestinationAccount, value);
    }

    public SavingsGoal? EditTransferDestinationGoal
    {
        get => editTransferDestinationGoal;
        set => SetProperty(ref editTransferDestinationGoal, value);
    }

    public CategoryChoiceViewModel? EditTransactionCategory
    {
        get => editTransactionCategory;
        set => SetProperty(ref editTransactionCategory, value);
    }

    public string EditTransactionCategoryName
    {
        get => editTransactionCategoryName;
        set => SetProperty(ref editTransactionCategoryName, value);
    }

    public string EditTransactionAmountText
    {
        get => editTransactionAmountText;
        set => SetProperty(ref editTransactionAmountText, value);
    }

    public string EditTransactionNotes
    {
        get => editTransactionNotes;
        set => SetProperty(ref editTransactionNotes, value);
    }

    public DateTime EditTransactionDate
    {
        get => editTransactionDate;
        set => SetProperty(ref editTransactionDate, value);
    }

    public TransactionType EditTransactionType
    {
        get => editTransactionType;
        set
        {
            if (SetProperty(ref editTransactionType, value))
            {
                UpdateCategoryChoices();
                EditTransactionCategory = EditTransactionCategoryChoices.FirstOrDefault();
                OnPropertyChanged(nameof(IsEditTransactionTransfer));
                OnPropertyChanged(nameof(IsEditTransactionNotTransfer));
                OnPropertyChanged(nameof(IsEditTransferToAccount));
                OnPropertyChanged(nameof(IsEditTransferToGoal));
            }
        }
    }

    public bool IsEditTransactionTransfer => EditTransactionType == TransactionType.Transfer;

    public bool IsEditTransactionNotTransfer => !IsEditTransactionTransfer;

    public Account? NewScheduledAccount
    {
        get => newScheduledAccount;
        set => SetProperty(ref newScheduledAccount, value);
    }

    public CategoryChoiceViewModel? SelectedScheduledCategory
    {
        get => selectedScheduledCategory;
        set => SetProperty(ref selectedScheduledCategory, value);
    }

    public string NewScheduledName
    {
        get => newScheduledName;
        set => SetProperty(ref newScheduledName, value);
    }

    public string NewScheduledAmountText
    {
        get => newScheduledAmountText;
        set => SetProperty(ref newScheduledAmountText, value);
    }

    public string NewScheduledNotes
    {
        get => newScheduledNotes;
        set => SetProperty(ref newScheduledNotes, value);
    }

    public DateTime NewScheduledDate
    {
        get => newScheduledDate;
        set
        {
            if (SetProperty(ref newScheduledDate, value))
            {
                OnPropertyChanged(nameof(ScheduledRecurrenceSentencePreview));
            }
        }
    }

    public TransactionType SelectedScheduledType
    {
        get => selectedScheduledType;
        set
        {
            if (SetProperty(ref selectedScheduledType, value))
            {
                UpdateCategoryChoices();
                SelectedScheduledCategory = ScheduledCategoryChoices.FirstOrDefault();
            }
        }
    }

    public RecurrenceFrequency SelectedScheduledFrequency
    {
        get => selectedScheduledFrequency;
        set
        {
            if (SetProperty(ref selectedScheduledFrequency, value))
            {
                OnPropertyChanged(nameof(ScheduledRecurrenceSentencePreview));
            }
        }
    }

    public ScheduledTransactionSummaryViewModel? SelectedScheduledForEditing
    {
        get => selectedScheduledForEditing;
        set
        {
            if (SetProperty(ref selectedScheduledForEditing, value))
            {
                LoadScheduledEditor(value?.Source);
            }
        }
    }

    public Account? EditScheduledAccount
    {
        get => editScheduledAccount;
        set => SetProperty(ref editScheduledAccount, value);
    }

    public CategoryChoiceViewModel? EditScheduledCategory
    {
        get => editScheduledCategory;
        set => SetProperty(ref editScheduledCategory, value);
    }

    public string EditScheduledCategoryName
    {
        get => editScheduledCategoryName;
        set => SetProperty(ref editScheduledCategoryName, value);
    }

    public string EditScheduledName
    {
        get => editScheduledName;
        set => SetProperty(ref editScheduledName, value);
    }

    public string EditScheduledAmountText
    {
        get => editScheduledAmountText;
        set => SetProperty(ref editScheduledAmountText, value);
    }

    public string EditScheduledNotes
    {
        get => editScheduledNotes;
        set => SetProperty(ref editScheduledNotes, value);
    }

    public DateTime EditScheduledDate
    {
        get => editScheduledDate;
        set => SetProperty(ref editScheduledDate, value);
    }

    public TransactionType EditScheduledType
    {
        get => editScheduledType;
        set
        {
            if (SetProperty(ref editScheduledType, value))
            {
                UpdateCategoryChoices();
                EditScheduledCategory = EditScheduledCategoryChoices.FirstOrDefault();
            }
        }
    }

    public RecurrenceFrequency EditScheduledFrequency
    {
        get => editScheduledFrequency;
        set => SetProperty(ref editScheduledFrequency, value);
    }

    public string EditScheduledIntervalText
    {
        get => editScheduledIntervalText;
        set => SetProperty(ref editScheduledIntervalText, value);
    }

    public string NewScheduledIntervalText
    {
        get => newScheduledIntervalText;
        set
        {
            if (SetProperty(ref newScheduledIntervalText, value))
            {
                OnPropertyChanged(nameof(ScheduledRecurrenceSentencePreview));
            }
        }
    }

    public string ScheduledRecurrenceSentencePreview
    {
        get
        {
            var interval = TryReadInt(NewScheduledIntervalText, out var parsedInterval) && parsedInterval > 0
                ? parsedInterval
                : 1;
            var period = RecurrenceDisplay.GetPeriodName(SelectedScheduledFrequency, interval);
            var startDate = DateDisplay.Format(DateOnly.FromDateTime(NewScheduledDate), SelectedDateDisplayFormat);

            return interval <= 1
                ? $"Every {period} starting {startDate}"
                : $"Every {interval} {period} starting {startDate}";
        }
    }

    public Account? NewGoalAccount
    {
        get => newGoalAccount;
        set => SetProperty(ref newGoalAccount, value);
    }

    public string NewGoalName
    {
        get => newGoalName;
        set => SetProperty(ref newGoalName, value);
    }

    public string NewGoalTargetAmountText
    {
        get => newGoalTargetAmountText;
        set => SetProperty(ref newGoalTargetAmountText, value);
    }

    public string NewGoalCurrentAmountText
    {
        get => newGoalCurrentAmountText;
        set => SetProperty(ref newGoalCurrentAmountText, value);
    }

    public DateTime NewGoalTargetDate
    {
        get => newGoalTargetDate;
        set => SetProperty(ref newGoalTargetDate, value);
    }

    public SavingsGoalSummaryViewModel? SelectedGoalForEditing
    {
        get => selectedGoalForEditing;
        set
        {
            if (SetProperty(ref selectedGoalForEditing, value))
            {
                LoadGoalEditor(value?.Source);
            }
        }
    }

    public Account? EditGoalAccount
    {
        get => editGoalAccount;
        set => SetProperty(ref editGoalAccount, value);
    }

    public string EditGoalName
    {
        get => editGoalName;
        set => SetProperty(ref editGoalName, value);
    }

    public string EditGoalTargetAmountText
    {
        get => editGoalTargetAmountText;
        set => SetProperty(ref editGoalTargetAmountText, value);
    }

    public string EditGoalCurrentAmountText
    {
        get => editGoalCurrentAmountText;
        set => SetProperty(ref editGoalCurrentAmountText, value);
    }

    public DateTime EditGoalTargetDate
    {
        get => editGoalTargetDate;
        set => SetProperty(ref editGoalTargetDate, value);
    }

    public string NewCategoryName
    {
        get => newCategoryName;
        set => SetProperty(ref newCategoryName, value);
    }

    public DateTime CheckInFromDate
    {
        get => checkInFromDate;
        set => SetProperty(ref checkInFromDate, value);
    }

    public DateTime CheckInToDate
    {
        get => checkInToDate;
        set => SetProperty(ref checkInToDate, value);
    }

    public bool HasCheckInExpectedTransactions => CheckInExpectedTransactions.Count > 0;

    public Account? ReconciliationAccount
    {
        get => reconciliationAccount;
        set
        {
            if (SetProperty(ref reconciliationAccount, value))
            {
                UpdateReconciliationAccountStatus();
                QueueReconciliationComparison();
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
                QueueReconciliationComparison();
            }
        }
    }

    public DateTime ActualBalanceDate
    {
        get => actualBalanceDate;
        set
        {
            if (SetProperty(ref actualBalanceDate, value))
            {
                QueueReconciliationComparison();
            }
        }
    }

    public string ReconciliationExpectedText
    {
        get => reconciliationExpectedText;
        private set => SetProperty(ref reconciliationExpectedText, value);
    }

    public string ReconciliationActualText
    {
        get => reconciliationActualText;
        private set => SetProperty(ref reconciliationActualText, value);
    }

    public string ReconciliationDifferenceText
    {
        get => reconciliationDifferenceText;
        private set => SetProperty(ref reconciliationDifferenceText, value);
    }

    public string ReconciliationStatusText
    {
        get => reconciliationStatusText;
        private set => SetProperty(ref reconciliationStatusText, value);
    }

    public string GroupedSpendingAmountText
    {
        get => groupedSpendingAmountText;
        set => SetProperty(ref groupedSpendingAmountText, value);
    }

    public string GroupedSpendingNotes
    {
        get => groupedSpendingNotes;
        set => SetProperty(ref groupedSpendingNotes, value);
    }

    public TransactionType SelectedReconciliationTransactionType
    {
        get => selectedReconciliationTransactionType;
        set
        {
            if (SetProperty(ref selectedReconciliationTransactionType, value))
            {
                UpdateCategoryChoices();
                SelectedGroupedSpendingCategory = GroupedSpendingCategoryChoices.FirstOrDefault();
            }
        }
    }

    public CategoryChoiceViewModel? SelectedGroupedSpendingCategory
    {
        get => selectedGroupedSpendingCategory;
        set => SetProperty(ref selectedGroupedSpendingCategory, value);
    }

    public string ReconciliationTransactionCategoryName
    {
        get => reconciliationTransactionCategoryName;
        set => SetProperty(ref reconciliationTransactionCategoryName, value);
    }

    public string ExportPathText
    {
        get => exportPathText;
        set => SetProperty(ref exportPathText, value);
    }

    public string ImportPathText
    {
        get => importPathText;
        set => SetProperty(ref importPathText, value);
    }

    public ImportMode SelectedImportMode
    {
        get => selectedImportMode;
        set => SetProperty(ref selectedImportMode, value);
    }

    public string ImportExportSummary
    {
        get => importExportSummary;
        private set => SetProperty(ref importExportSummary, value);
    }

    public Account? StatementImportAccount
    {
        get => statementImportAccount;
        set
        {
            if (SetProperty(ref statementImportAccount, value))
            {
                StatementMatchedAccountText = value is null
                    ? "No account matched yet."
                    : $"Matched account: {value.Name}";
                OnPropertyChanged(nameof(HasStatementMatchedAccount));
            }
        }
    }

    public string StatementImportPathText
    {
        get => statementImportPathText;
        set
        {
            if (SetProperty(ref statementImportPathText, value))
            {
                StatementSelectedFileText = string.IsNullOrWhiteSpace(value)
                    ? "No file selected."
                    : Path.GetFileName(value);
                UpdateStatementParserStatus();
            }
        }
    }

    public Account? StatementConnectAccount
    {
        get => statementConnectAccount;
        set => SetProperty(ref statementConnectAccount, value);
    }

    public string StatementNewAccountName
    {
        get => statementNewAccountName;
        set => SetProperty(ref statementNewAccountName, value);
    }

    public string StatementNewAccountCurrency
    {
        get => statementNewAccountCurrency;
        set => SetProperty(ref statementNewAccountCurrency, NormalizeCurrency(value));
    }

    public string StatementSelectedFileText
    {
        get => statementSelectedFileText;
        private set => SetProperty(ref statementSelectedFileText, value);
    }

    public string StatementDetectedAccountText
    {
        get => statementDetectedAccountText;
        private set => SetProperty(ref statementDetectedAccountText, value);
    }

    public string StatementDetectedCardText
    {
        get => statementDetectedCardText;
        private set => SetProperty(ref statementDetectedCardText, value);
    }

    public string StatementMatchedAccountText
    {
        get => statementMatchedAccountText;
        private set => SetProperty(ref statementMatchedAccountText, value);
    }

    public bool IsStatementAccountUnmatched
    {
        get => isStatementAccountUnmatched;
        private set => SetProperty(ref isStatementAccountUnmatched, value);
    }

    public bool HasStatementMatchedAccount => StatementImportAccount is not null;

    public string StatementImportStatusText
    {
        get => statementImportStatusText;
        private set => SetProperty(ref statementImportStatusText, value);
    }

    public string AvailableStatementParsersText => statementParserRegistry.AvailableParsers.Count == 0
        ? "No bank-specific parsers are installed yet."
        : $"{statementParserRegistry.AvailableParsers.Count} parser(s) available.";

    public StatementImportBatchSummaryViewModel? SelectedStatementImportBatch
    {
        get => selectedStatementImportBatch;
        set
        {
            if (SetProperty(ref selectedStatementImportBatch, value))
            {
                _ = LoadSelectedStatementRowsAsync();
            }
        }
    }

    public StatementImportRowSummaryViewModel? SelectedStatementImportRow
    {
        get => selectedStatementImportRow;
        set
        {
            if (SetProperty(ref selectedStatementImportRow, value))
            {
                UpdateStatementImportCategoryChoices();
            }
        }
    }

    public CategoryChoiceViewModel? SelectedStatementImportCategory
    {
        get => selectedStatementImportCategory;
        set => SetProperty(ref selectedStatementImportCategory, value);
    }

    public string StatementImportCategoryName
    {
        get => statementImportCategoryName;
        set => SetProperty(ref statementImportCategoryName, value);
    }

    public bool HasStatementImportBatches => StatementImportBatchSummaries.Count > 0;

    public bool HasStatementImportRows => StatementImportRows.Count > 0;

    public string DeleteAllConfirmationText
    {
        get => deleteAllConfirmationText;
        set => SetProperty(ref deleteAllConfirmationText, value);
    }

    public bool DeleteAllBackupAcknowledged
    {
        get => deleteAllBackupAcknowledged;
        set => SetProperty(ref deleteAllBackupAcknowledged, value);
    }

    public async Task LoadAsync()
    {
        if (isLoaded)
        {
            return;
        }

        await RefreshAsync();
        isLoaded = true;
    }

    public async Task RefreshAsync()
    {
        await RunBusyAsync(async () =>
        {
            var settings = await settingsRepository.GetAsync();
            DefaultCurrency = settings.DefaultCurrency;
            NewAccountCurrency = settings.DefaultCurrency;
            selectedForecastPeriod = settings.DefaultForecastPeriod;
            OnPropertyChanged(nameof(SelectedForecastPeriod));
            SelectedReminderFrequency = settings.ReminderFrequency;
            selectedDateDisplayFormat = settings.DateDisplayFormat;
            OnPropertyChanged(nameof(SelectedDateDisplayFormat));

            Replace(Accounts, await accountRepository.GetAllAsync());
            Replace(Categories, await categoryRepository.GetAllAsync());
            Replace(Transactions, await transactionRepository.GetAllAsync());
            Replace(ScheduledTransactions, await scheduledTransactionRepository.GetAllAsync());
            Replace(SavingsGoals, await savingsGoalRepository.GetAllAsync());

            UpdateCategoryChoices();
            EnsureDefaultSelections();
            UpdateSummaries();
            UpdateForecast();
            await RefreshStatementImportsCoreAsync();
            SetStatus("Loaded saved local data.", clearAutomatically: true);
        });
    }

    private async Task AddAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAccountName))
        {
            SetStatus("Give the account a name first.");
            return;
        }

        if (!TryReadDecimal(NewAccountBalanceText, out var balance))
        {
            SetStatus("Account balance must be a number.");
            return;
        }

        if (!TryNormalizeCardLastFour(NewCardLastFourDigits, out var cardLastFourDigits))
        {
            SetStatus("Card last four must be exactly four digits when entered.");
            return;
        }

        var creditCardDetails = CreateCreditCardDetailsForNewAccount();
        var account = new Account(
            Guid.NewGuid(),
            NewAccountName.Trim(),
            SelectedAccountType,
            balance,
            NormalizeCurrency(NewAccountCurrency),
            DateTimeOffset.UtcNow,
            IsArchived: false,
            creditCardDetails,
            NewAccountIncludeInDashboardTotals,
            CleanOptionalText(NewAccountNumber),
            IsCardAccountType(SelectedAccountType) ? cardLastFourDigits : null);

        await accountRepository.SaveAsync(account);
        NewAccountName = string.Empty;
        NewAccountBalanceText = "0";
        NewAccountNumber = string.Empty;
        NewCardLastFourDigits = string.Empty;
        NewAccountIncludeInDashboardTotals = SelectedAccountType != AccountType.CreditCard;
        NewCreditCardDebtText = string.Empty;
        NewCreditCardMinimumPaymentText = string.Empty;
        NewCreditCardPlannedPaymentText = string.Empty;
        NewCreditCardPaymentDueDayText = string.Empty;
        await RefreshAfterMutationAsync("Account saved.");
    }

    private async Task SaveSelectedAccountAsync()
    {
        if (SelectedAccount is null)
        {
            SetStatus("Select an account to update.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedAccountName))
        {
            SetStatus("Give the account a name first.");
            return;
        }

        if (!TryNormalizeCardLastFour(SelectedCardLastFourDigits, out var cardLastFourDigits))
        {
            SetStatus("Card last four must be exactly four digits when entered.");
            return;
        }

        var updatedAccount = SelectedAccount with
        {
            Name = SelectedAccountName.Trim(),
            Type = SelectedAccountEditType,
            Currency = NormalizeCurrency(SelectedAccountCurrency),
            CreditCardDetails = CreateCreditCardDetailsForSelectedAccount(),
            IncludeInDashboardTotals = SelectedAccountIncludeInDashboardTotals,
            AccountNumber = CleanOptionalText(SelectedAccountNumber),
            CardLastFourDigits = IsCardAccountType(SelectedAccountEditType) ? cardLastFourDigits : null
        };

        await accountRepository.SaveAsync(updatedAccount);
        await RefreshAfterMutationAsync("Account updated.");
    }

    private async Task DeleteAccountAsync(Account account)
    {
        await accountRepository.DeleteAsync(account.Id);
        await RefreshAfterMutationAsync("Account deleted.");
    }

    private async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            SetStatus("Give the category a name first.");
            return;
        }

        if (Categories.Any(category => string.Equals(category.Name, NewCategoryName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus("That category already exists.");
            return;
        }

        var category = new Category(Guid.NewGuid(), NewCategoryName.Trim(), SelectedTransactionType);
        await categoryRepository.SaveAsync(category);
        NewCategoryName = string.Empty;
        await RefreshAfterMutationAsync("Category saved.");
    }

    private async Task DeleteCategoryAsync(Category category)
    {
        await categoryRepository.DeleteAsync(category.Id);
        await RefreshAfterMutationAsync("Category deleted.");
    }

    private async Task<Guid?> ResolveCategoryChoiceAsync(CategoryChoiceViewModel? categoryChoice, TransactionType type)
    {
        if (categoryChoice is null || categoryChoice.IsNone)
        {
            return null;
        }

        if (categoryChoice.CategoryId.HasValue)
        {
            return categoryChoice.CategoryId.Value;
        }

        var existing = Categories.FirstOrDefault(category =>
            string.Equals(category.Name, categoryChoice.Name, StringComparison.OrdinalIgnoreCase)
            && (category.Type is null || category.Type == type));
        if (existing is not null)
        {
            return existing.Id;
        }

        var category = new Category(Guid.NewGuid(), categoryChoice.Name, type);
        await categoryRepository.SaveAsync(category);
        return category.Id;
    }

    private async Task<Guid?> ResolveCategoryAsync(
        string newCategoryName,
        CategoryChoiceViewModel? categoryChoice,
        TransactionType type)
    {
        if (string.IsNullOrWhiteSpace(newCategoryName))
        {
            return await ResolveCategoryChoiceAsync(categoryChoice, type);
        }

        var trimmedName = newCategoryName.Trim();
        var existing = Categories.FirstOrDefault(category =>
            string.Equals(category.Name, trimmedName, StringComparison.OrdinalIgnoreCase)
            && (category.Type is null || category.Type == type));
        if (existing is not null)
        {
            return existing.Id;
        }

        var category = new Category(Guid.NewGuid(), trimmedName, type);
        await categoryRepository.SaveAsync(category);
        return category.Id;
    }

    private async Task AddTransactionAsync()
    {
        if (NewTransactionAccount is null)
        {
            SetStatus("Create or choose an account before adding a transaction.");
            return;
        }

        if (!TryReadDecimal(NewTransactionAmountText, out var amount) || amount <= 0m)
        {
            SetStatus("Transaction amount must be greater than zero.");
            return;
        }

        Guid? destinationAccountId = null;
        Guid? destinationGoalId = null;
        if (SelectedTransactionType == TransactionType.Transfer)
        {
            if (SelectedTransferDestinationKind == TransferDestinationKind.Account)
            {
                if (NewTransferDestinationAccount is null)
                {
                    SetStatus("Choose a destination account for this transfer.");
                    return;
                }

                if (NewTransferDestinationAccount.Id == NewTransactionAccount.Id)
                {
                    SetStatus("Transfer destination must be different from the source account.");
                    return;
                }

                destinationAccountId = NewTransferDestinationAccount.Id;
            }
            else
            {
                if (NewTransferDestinationGoal is null)
                {
                    SetStatus("Choose a destination goal for this transfer.");
                    return;
                }

                destinationGoalId = NewTransferDestinationGoal.Id;
            }
        }

        var categoryId = SelectedTransactionType == TransactionType.Transfer
            ? null
            : await ResolveCategoryAsync(NewTransactionCategoryName, SelectedTransactionCategory, SelectedTransactionType);
        var transaction = new Transaction(
            Guid.NewGuid(),
            DateOnly.FromDateTime(NewTransactionDate),
            amount,
            NewTransactionAccount.Id,
            categoryId,
            string.IsNullOrWhiteSpace(NewTransactionNotes) ? null : NewTransactionNotes.Trim(),
            SelectedTransactionType,
            destinationAccountId,
            destinationGoalId);

        await SaveAppliedTransactionAsync(transaction);
        NewTransactionAmountText = string.Empty;
        NewTransactionNotes = string.Empty;
        NewTransactionCategoryName = string.Empty;
        await RefreshAfterMutationAsync("Transaction saved.");
    }

    private async Task DeleteTransactionAsync(Transaction transaction)
    {
        await ReverseAppliedTransactionAsync(transaction);
        await transactionRepository.DeleteAsync(transaction.Id);
        await RefreshAfterMutationAsync("Transaction deleted.");
    }

    private async Task SaveEditedTransactionAsync()
    {
        var original = SelectedTransactionForEditing?.Source;
        if (original is null)
        {
            SetStatus("Select a transaction to edit.");
            return;
        }

        if (EditTransactionAccount is null)
        {
            SetStatus("Choose an account for the edited transaction.");
            return;
        }

        if (!TryReadDecimal(EditTransactionAmountText, out var amount) || amount <= 0m)
        {
            SetStatus("Edited transaction amount must be greater than zero.");
            return;
        }

        Guid? destinationAccountId = null;
        Guid? destinationGoalId = null;
        if (EditTransactionType == TransactionType.Transfer)
        {
            if (EditTransferDestinationKind == TransferDestinationKind.Account)
            {
                if (EditTransferDestinationAccount is null)
                {
                    SetStatus("Choose a destination account for this transfer.");
                    return;
                }

                if (EditTransferDestinationAccount.Id == EditTransactionAccount.Id)
                {
                    SetStatus("Transfer destination must be different from the source account.");
                    return;
                }

                destinationAccountId = EditTransferDestinationAccount.Id;
            }
            else
            {
                if (EditTransferDestinationGoal is null)
                {
                    SetStatus("Choose a destination goal for this transfer.");
                    return;
                }

                destinationGoalId = EditTransferDestinationGoal.Id;
            }
        }

        var categoryId = EditTransactionType == TransactionType.Transfer
            ? null
            : await ResolveCategoryAsync(EditTransactionCategoryName, EditTransactionCategory, EditTransactionType);
        var updated = original with
        {
            Date = DateOnly.FromDateTime(EditTransactionDate),
            Amount = amount,
            AccountId = EditTransactionAccount.Id,
            CategoryId = categoryId,
            Notes = string.IsNullOrWhiteSpace(EditTransactionNotes) ? null : EditTransactionNotes.Trim(),
            Type = EditTransactionType,
            DestinationAccountId = destinationAccountId,
            DestinationGoalId = destinationGoalId
        };

        await ReverseAppliedTransactionAsync(original);
        await SaveAppliedTransactionAsync(updated);
        await RefreshAfterMutationAsync("Transaction updated.");
    }

    private async Task AddScheduledTransactionAsync()
    {
        if (NewScheduledAccount is null)
        {
            SetStatus("Create or choose an account before adding a scheduled item.");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewScheduledName))
        {
            SetStatus("Give the scheduled item a name first.");
            return;
        }

        if (!TryReadDecimal(NewScheduledAmountText, out var amount) || amount <= 0m)
        {
            SetStatus("Scheduled amount must be greater than zero.");
            return;
        }

        if (!TryReadInt(NewScheduledIntervalText, out var interval) || interval < 1)
        {
            SetStatus("Recurrence interval must be at least 1.");
            return;
        }

        var startDate = DateOnly.FromDateTime(NewScheduledDate);
        var recurrenceRule = CreateRecurrenceRule(SelectedScheduledFrequency, interval, startDate);
        var categoryId = await ResolveCategoryChoiceAsync(SelectedScheduledCategory, SelectedScheduledType);
        var scheduledTransaction = new ScheduledTransaction(
            Guid.NewGuid(),
            NewScheduledName.Trim(),
            amount,
            NewScheduledAccount.Id,
            categoryId,
            SelectedScheduledType,
            recurrenceRule,
            startDate,
            Active: true,
            string.IsNullOrWhiteSpace(NewScheduledNotes) ? null : NewScheduledNotes.Trim());

        await scheduledTransactionRepository.SaveAsync(scheduledTransaction);
        NewScheduledName = string.Empty;
        NewScheduledAmountText = string.Empty;
        NewScheduledNotes = string.Empty;
        await RefreshAfterMutationAsync("Scheduled item saved.");
    }

    private async Task DeleteScheduledTransactionAsync(ScheduledTransaction scheduledTransaction)
    {
        await scheduledTransactionRepository.DeleteAsync(scheduledTransaction.Id);
        await RefreshAfterMutationAsync("Scheduled item deleted.");
    }

    private async Task SaveEditedScheduledTransactionAsync()
    {
        var original = SelectedScheduledForEditing?.Source;
        if (original is null)
        {
            SetStatus("Select a scheduled item to edit.");
            return;
        }

        if (EditScheduledAccount is null)
        {
            SetStatus("Choose an account for the scheduled item.");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditScheduledName))
        {
            SetStatus("Give the scheduled item a name.");
            return;
        }

        if (!TryReadDecimal(EditScheduledAmountText, out var amount) || amount <= 0m)
        {
            SetStatus("Scheduled amount must be greater than zero.");
            return;
        }

        if (!TryReadInt(EditScheduledIntervalText, out var interval) || interval < 1)
        {
            SetStatus("Recurrence interval must be at least 1.");
            return;
        }

        var startDate = DateOnly.FromDateTime(EditScheduledDate);
        var categoryId = await ResolveCategoryAsync(EditScheduledCategoryName, EditScheduledCategory, EditScheduledType);
        var updated = original with
        {
            Name = EditScheduledName.Trim(),
            Amount = amount,
            AccountId = EditScheduledAccount.Id,
            CategoryId = categoryId,
            Type = EditScheduledType,
            RecurrenceRule = CreateRecurrenceRule(EditScheduledFrequency, interval, startDate),
            NextOccurrence = startDate,
            Active = true,
            Notes = string.IsNullOrWhiteSpace(EditScheduledNotes) ? null : EditScheduledNotes.Trim()
        };

        await scheduledTransactionRepository.SaveAsync(updated);
        await RefreshAfterMutationAsync("Scheduled item updated.");
    }

    private async Task AddSavingsGoalAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGoalName))
        {
            SetStatus("Give the goal a name first.");
            return;
        }

        if (!TryReadDecimal(NewGoalTargetAmountText, out var targetAmount) || targetAmount <= 0m)
        {
            SetStatus("Goal target must be greater than zero.");
            return;
        }

        if (!TryReadDecimal(NewGoalCurrentAmountText, out var currentAmount) || currentAmount < 0m)
        {
            SetStatus("Goal current amount must be zero or more.");
            return;
        }

        var savingsGoal = new SavingsGoal(
            Guid.NewGuid(),
            NewGoalName.Trim(),
            targetAmount,
            currentAmount,
            DateOnly.FromDateTime(NewGoalTargetDate),
            NewGoalAccount?.Id);

        await savingsGoalRepository.SaveAsync(savingsGoal);
        NewGoalName = string.Empty;
        NewGoalTargetAmountText = string.Empty;
        NewGoalCurrentAmountText = string.Empty;
        await RefreshAfterMutationAsync("Savings goal saved.");
    }

    private async Task DeleteSavingsGoalAsync(SavingsGoal savingsGoal)
    {
        await savingsGoalRepository.DeleteAsync(savingsGoal.Id);
        await RefreshAfterMutationAsync("Savings goal deleted.");
    }

    private async Task SaveEditedSavingsGoalAsync()
    {
        var original = SelectedGoalForEditing?.Source;
        if (original is null)
        {
            SetStatus("Select a goal to edit.");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditGoalName))
        {
            SetStatus("Give the goal a name.");
            return;
        }

        if (!TryReadDecimal(EditGoalTargetAmountText, out var targetAmount) || targetAmount <= 0m)
        {
            SetStatus("Goal target must be greater than zero.");
            return;
        }

        if (!TryReadDecimal(EditGoalCurrentAmountText, out var currentAmount) || currentAmount < 0m)
        {
            SetStatus("Goal current amount must be zero or more.");
            return;
        }

        await savingsGoalRepository.SaveAsync(original with
        {
            Name = EditGoalName.Trim(),
            TargetAmount = targetAmount,
            CurrentAmount = currentAmount,
            TargetDate = DateOnly.FromDateTime(EditGoalTargetDate),
            AccountId = EditGoalAccount?.Id
        });
        await RefreshAfterMutationAsync("Savings goal updated.");
    }

    private async Task CreateCheckInAsync()
    {
        await RunBusyAsync(() =>
        {
            var fromDate = DateOnly.FromDateTime(CheckInFromDate);
            var toDate = DateOnly.FromDateTime(CheckInToDate);
            var session = checkInService.CreateSession(fromDate, toDate, ScheduledTransactions);

            Replace(CheckInExpectedTransactions, session.ExpectedTransactions.Select(review =>
                new ExpectedTransactionReviewViewModel(
                    review,
                    Accounts.FirstOrDefault(account => account.Id == review.ExpectedEvent.AccountId)?.Name ?? "Unknown account",
                    Categories.FirstOrDefault(category => category.Id == review.ExpectedEvent.CategoryId)?.Name,
                    DefaultCurrency,
                    SelectedDateDisplayFormat)));
            OnPropertyChanged(nameof(HasCheckInExpectedTransactions));
            SetStatus($"Loaded {CheckInExpectedTransactions.Count} expected item(s) for check-in.", clearAutomatically: true);
            return Task.CompletedTask;
        });
    }

    private async Task ConfirmExpectedTransactionAsync(ExpectedTransactionReviewViewModel item)
    {
        if (item.Source.Decision != ExpectedTransactionDecision.Pending)
        {
            SetStatus("That expected item has already been reviewed.");
            return;
        }

        var forecastEvent = item.Source.ExpectedEvent;
        if (!TryReadDecimal(item.EditableAmountText, out var editedAmount) || editedAmount <= 0m)
        {
            SetStatus("Expected item amount must be greater than zero.");
            return;
        }

        var transaction = new Transaction(
            Guid.NewGuid(),
            forecastEvent.Date,
            editedAmount,
            forecastEvent.AccountId,
            forecastEvent.CategoryId,
            forecastEvent.Name,
            forecastEvent.Type);

        await SaveAppliedTransactionAsync(transaction);
        await AdvanceScheduledTransactionAsync(forecastEvent.SourceId, forecastEvent.Date);
        item.MarkConfirmed();
        await RefreshAsync();
        SetStatus("Expected item confirmed and recorded.", clearAutomatically: true);
    }

    private async Task DelayExpectedTransactionAsync(ExpectedTransactionReviewViewModel item)
    {
        if (item.Source.Decision != ExpectedTransactionDecision.Pending)
        {
            SetStatus("That expected item has already been reviewed.");
            return;
        }

        var delayedUntil = item.Source.ExpectedEvent.Date.AddDays(1);
        var scheduledTransaction = await scheduledTransactionRepository.GetByIdAsync(item.Source.ExpectedEvent.SourceId);
        if (scheduledTransaction is null)
        {
            SetStatus("Could not find that scheduled item.");
            return;
        }

        await scheduledTransactionRepository.SaveAsync(scheduledTransaction with
        {
            NextOccurrence = delayedUntil,
            Active = true
        });
        item.MarkDelayed(delayedUntil, SelectedDateDisplayFormat);
        await RefreshAsync();
        SetStatus("Expected item delayed by one day.", clearAutomatically: true);
    }

    private async Task CancelExpectedTransactionAsync(ExpectedTransactionReviewViewModel item)
    {
        if (item.Source.Decision != ExpectedTransactionDecision.Pending)
        {
            SetStatus("That expected item has already been reviewed.");
            return;
        }

        await AdvanceScheduledTransactionAsync(item.Source.ExpectedEvent.SourceId, item.Source.ExpectedEvent.Date);
        item.MarkCancelled();
        await RefreshAsync();
        SetStatus("Expected item skipped.", clearAutomatically: true);
    }

    private async Task CompareRealityAsync()
    {
        await CompareRealityNowAsync(showStatus: true);
    }

    private async Task CompareRealityNowAsync(bool showStatus)
    {
        await Task.Yield();

        if (ReconciliationAccount is null)
        {
            latestReconciliationResult = null;
            ReconciliationExpectedText = "Choose an account.";
            ReconciliationActualText = "Not entered yet.";
            ReconciliationDifferenceText = "Not compared yet.";
            ReconciliationStatusText = "Waiting";
            return;
        }

        ReconciliationExpectedText = FormatMoney(ReconciliationAccount.CurrentBalance, ReconciliationAccount.Currency);

        if (string.IsNullOrWhiteSpace(ActualBalanceText))
        {
            latestReconciliationResult = null;
            ReconciliationActualText = "Not entered yet.";
            ReconciliationDifferenceText = "Not compared yet.";
            ReconciliationStatusText = "Waiting for real balance";
            return;
        }

        if (!TryReadDecimal(ActualBalanceText, out var actualBalance))
        {
            latestReconciliationResult = null;
            ReconciliationActualText = "Invalid number";
            ReconciliationDifferenceText = "Not compared yet.";
            ReconciliationStatusText = "Check the real balance value";
            return;
        }

        var actualDate = DateOnly.FromDateTime(ActualBalanceDate);
        latestReconciliationResult = reconciliationService.Compare(
            ReconciliationAccount.CurrentBalance,
            actualBalance,
            actualDate);
        ReconciliationActualText = FormatMoney(latestReconciliationResult.ActualBalance, ReconciliationAccount.Currency);
        ReconciliationDifferenceText = FormatMoney(latestReconciliationResult.Difference, ReconciliationAccount.Currency);
        ReconciliationStatusText = DisplayText.Format(latestReconciliationResult.Status);
        if (showStatus)
        {
            SetStatus("Reality compared with account balance.", clearAutomatically: true);
        }
    }

    private void QueueReconciliationComparison()
    {
        reconciliationComparisonCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        reconciliationComparisonCancellation = cancellation;
        _ = CompareRealityAfterDelayAsync(cancellation.Token);
    }

    private async Task CompareRealityAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                await CompareRealityNowAsync(showStatus: false);
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void UpdateReconciliationAccountStatus()
    {
        if (ReconciliationAccount is null)
        {
            ReconciliationExpectedText = "Choose an account.";
            ReconciliationActualText = "Not entered yet.";
            ReconciliationDifferenceText = "Not compared yet.";
            ReconciliationStatusText = "Waiting";
            return;
        }

        ReconciliationExpectedText = FormatMoney(ReconciliationAccount.CurrentBalance, ReconciliationAccount.Currency);
        ReconciliationActualText = string.IsNullOrWhiteSpace(ActualBalanceText)
            ? "Not entered yet."
            : ReconciliationActualText;
        ReconciliationDifferenceText = string.IsNullOrWhiteSpace(ActualBalanceText)
            ? "Not compared yet."
            : ReconciliationDifferenceText;
        ReconciliationStatusText = string.IsNullOrWhiteSpace(ActualBalanceText)
            ? "Waiting for real balance"
            : ReconciliationStatusText;
    }

    private async Task AddGroupedSpendingAsync()
    {
        if (ReconciliationAccount is null)
        {
            SetStatus("Choose the account for this reconciliation transaction.");
            return;
        }

        if (!TryReadDecimal(GroupedSpendingAmountText, out var amount) || amount <= 0m)
        {
            SetStatus("Reconciliation transaction amount must be greater than zero.");
            return;
        }

        if (SelectedReconciliationTransactionType == TransactionType.Transfer)
        {
            SetStatus("Use the Transactions tab for transfers.");
            return;
        }

        var categoryId = await ResolveCategoryAsync(
            ReconciliationTransactionCategoryName,
            SelectedGroupedSpendingCategory,
            SelectedReconciliationTransactionType);
        var transaction = new Transaction(
            Guid.NewGuid(),
            DateOnly.FromDateTime(ActualBalanceDate),
            amount,
            ReconciliationAccount.Id,
            categoryId,
            string.IsNullOrWhiteSpace(GroupedSpendingNotes) ? "Reconciliation transaction" : GroupedSpendingNotes.Trim(),
            SelectedReconciliationTransactionType);

        await SaveAppliedTransactionAsync(transaction);
        GroupedSpendingAmountText = string.Empty;
        GroupedSpendingNotes = string.Empty;
        ReconciliationTransactionCategoryName = string.Empty;
        await RefreshAsync();
        QueueReconciliationComparison();
        SetStatus("Reconciliation transaction saved.", clearAutomatically: true);
    }

    private async Task AddBalanceAdjustmentAsync()
    {
        if (ReconciliationAccount is null)
        {
            SetStatus("Choose the account to adjust.");
            return;
        }

        if (latestReconciliationResult is null)
        {
            SetStatus("Compare reality first, then choose whether to adjust.");
            return;
        }

        if (latestReconciliationResult.Difference == 0m)
        {
            SetStatus("No adjustment is needed.");
            return;
        }

        var transaction = balanceAdjustmentService.CreateTransaction(new BalanceAdjustment(
            latestReconciliationResult.Date,
            ReconciliationAccount.Id,
            latestReconciliationResult.Difference,
            "Reconciliation balance adjustment"));

        await SaveAppliedTransactionAsync(transaction);
        latestReconciliationResult = null;
        await RefreshAsync();
        SetStatus("Balance adjustment saved.", clearAutomatically: true);
    }

    private async Task CreateBackupAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(ExportPathText))
            {
                SetStatus("Enter an export path first.");
                return;
            }

            await backupService.CreateBackupAsync(ExportPathText.Trim());
            ImportExportSummary = $"Backup created: {ExportPathText.Trim()}";
            SetStatus("Backup created.", clearAutomatically: true);
        });
    }

    private async Task ValidateImportAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(ImportPathText))
            {
                SetStatus("Enter an import path first.");
                return;
            }

            var exportEnvelope = await backupService.ReadBackupAsync(ImportPathText.Trim());
            var validation = await importService.ValidateAsync(exportEnvelope);
            ImportExportSummary = validation.IsValid
                ? $"Valid Banccoon export v{exportEnvelope.ExportFormatVersion}. Contains {exportEnvelope.Data.Accounts.Count} account(s), {exportEnvelope.Data.Transactions.Count} transaction(s), {exportEnvelope.Data.ScheduledTransactions.Count} scheduled item(s), {exportEnvelope.Data.Categories.Count} categor(ies), {exportEnvelope.Data.SavingsGoals.Count} goal(s), and {exportEnvelope.Data.StatementImportBatches.Count} statement import(s)."
                : $"Import is not valid: {string.Join(" ", validation.Errors)}";
            SetStatus(validation.IsValid ? "Import file is valid." : "Import validation failed.", clearAutomatically: validation.IsValid);
        });
    }

    private async Task RestoreBackupAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(ImportPathText))
            {
                SetStatus("Enter an import path first.");
                return;
            }

            var result = await backupService.RestoreBackupAsync(ImportPathText.Trim(), SelectedImportMode);
            ImportExportSummary = FormatImportResult(result);
            if (result.Validation.IsValid && result.Mode != ImportMode.ValidateOnly)
            {
                await RefreshAsync();
            }

            SetStatus(result.Validation.IsValid ? "Import action completed." : "Import failed validation.", clearAutomatically: result.Validation.IsValid);
        });
    }

    private async Task PickStatementFileAsync()
    {
        await RunBusyAsync(async () =>
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a bank statement"
            });

            if (result is null)
            {
                SetStatus("Statement import cancelled.");
                return;
            }

            if (string.IsNullOrWhiteSpace(result.FullPath))
            {
                SetStatus("The selected file did not provide a local path.");
                return;
            }

            await PreviewAndResolveStatementAsync(result.FullPath);
        });
    }

    private async Task AnalyzeStatementAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (StatementImportAccount is null)
            {
                SetStatus("Import a statement file first so Banccoon can match the account.");
                return;
            }

            await CreatePendingStatementImportAsync(
                StatementImportAccount,
                pendingStatementPreview,
                pendingStatementFilePath);
        });
    }

    private async Task ConnectStatementAccountAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (pendingStatementPreview is null || string.IsNullOrWhiteSpace(pendingStatementFilePath))
            {
                SetStatus("Import a statement file before linking an account.");
                return;
            }

            if (StatementConnectAccount is null)
            {
                SetStatus("Choose an existing account to link.");
                return;
            }

            var accountNumber = NormalizeAccountNumber(pendingStatementPreview.AccountNumber);
            var updatedAccount = StatementConnectAccount with
            {
                AccountNumber = string.IsNullOrWhiteSpace(accountNumber)
                    ? StatementConnectAccount.AccountNumber
                    : accountNumber,
                CardLastFourDigits = string.IsNullOrWhiteSpace(pendingStatementPreview.CardLastFourDigits)
                    ? StatementConnectAccount.CardLastFourDigits
                    : pendingStatementPreview.CardLastFourDigits
            };

            await accountRepository.SaveAsync(updatedAccount);
            await RefreshAccountsAfterStatementAccountChangeAsync(updatedAccount.Id);

            if (StatementImportAccount is null)
            {
                SetStatus("The linked account could not be reloaded.");
                return;
            }

            await CreatePendingStatementImportAsync(
                StatementImportAccount,
                pendingStatementPreview,
                pendingStatementFilePath);
        });
    }

    private async Task CreateStatementAccountFromStatementAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (pendingStatementPreview is null || string.IsNullOrWhiteSpace(pendingStatementFilePath))
            {
                SetStatus("Import a statement file before creating an account.");
                return;
            }

            if (string.IsNullOrWhiteSpace(StatementNewAccountName))
            {
                SetStatus("Name the new statement account first.");
                return;
            }

            var account = new Account(
                Guid.NewGuid(),
                StatementNewAccountName.Trim(),
                AccountType.DebitCard,
                pendingStatementPreview.OpeningBalance ?? 0m,
                NormalizeCurrency(StatementNewAccountCurrency),
                DateTimeOffset.UtcNow,
                IsArchived: false,
                CreditCardDetails: null,
                IncludeInDashboardTotals: true,
                AccountNumber: CleanOptionalText(NormalizeAccountNumber(pendingStatementPreview.AccountNumber)),
                CardLastFourDigits: CleanOptionalText(pendingStatementPreview.CardLastFourDigits));

            await accountRepository.SaveAsync(account);
            await RefreshAccountsAfterStatementAccountChangeAsync(account.Id);

            if (StatementImportAccount is null)
            {
                SetStatus("The new account could not be loaded.");
                return;
            }

            await CreatePendingStatementImportAsync(
                StatementImportAccount,
                pendingStatementPreview,
                pendingStatementFilePath);
        });
    }

    private async Task RefreshStatementImportsAsync()
    {
        await RunBusyAsync(async () =>
        {
            await RefreshStatementImportsCoreAsync(SelectedStatementImportBatch?.Source.Id);
            SetStatus("Statement imports refreshed.", clearAutomatically: true);
        });
    }

    private async Task CancelStatementImportAsync()
    {
        if (SelectedStatementImportBatch is null)
        {
            SetStatus("No statement import is selected.");
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await statementImportService.CancelImportAsync(SelectedStatementImportBatch.Source.Id);
            StatementImportStatusText = result.Message;
            if (result.Cancelled)
            {
                ClearPendingStatementResolution();
                await RefreshStatementImportsCoreAsync();
                SetStatus(result.Message, clearAutomatically: true);
                return;
            }

            SetStatus(result.Message);
        });
    }

    private async Task ApproveStatementRowAsync(StatementImportRowSummaryViewModel row)
    {
        if (row.Source.Status != StatementImportRowStatus.Pending)
        {
            SetStatus("That statement row has already been reviewed.");
            return;
        }

        await RunBusyAsync(async () =>
        {
            var categoryId = await ResolveCategoryChoiceAsync(row.SelectedCategory, row.Source.Type);
            await statementImportService.ApproveRowAsync(row.Source.Id, categoryId);
            await RefreshAsync();
            SetStatus("Statement row imported as a transaction.", clearAutomatically: true);
        });
    }

    private async Task SkipStatementRowAsync(StatementImportRowSummaryViewModel row)
    {
        if (row.Source.Status != StatementImportRowStatus.Pending)
        {
            SetStatus("That statement row has already been reviewed.");
            return;
        }

        await RunBusyAsync(async () =>
        {
            await statementImportService.SkipRowAsync(row.Source.Id);
            await RefreshStatementImportsCoreAsync(SelectedStatementImportBatch?.Source.Id ?? row.Source.BatchId);
            SetStatus("Statement row skipped.", clearAutomatically: true);
        });
    }

    private async Task PreviewAndResolveStatementAsync(string filePath)
    {
        ResetPendingStatementResolution(filePath);
        StatementImportStatusText = "Reading statement...";

        var preview = await statementImportService.PreviewAsync(filePath);
        StatementImportStatusText = preview.Message;
        if (!preview.ParserAvailable || preview.Statement is null)
        {
            SetStatus(preview.Message);
            return;
        }

        pendingStatementPreview = preview.Statement;
        pendingStatementFilePath = filePath;
        StatementDetectedAccountText = FormatDetectedAccountText(preview.Statement.AccountNumber);
        StatementDetectedCardText = FormatDetectedCardText(preview.Statement.CardLastFourDigits);
        StatementNewAccountName = CreateDefaultStatementAccountName(preview.Statement);
        StatementNewAccountCurrency = "RUB";

        var matchedAccount = FindAccountByAccountNumber(preview.Statement.AccountNumber);
        if (matchedAccount is not null)
        {
            IsStatementAccountUnmatched = false;
            StatementImportAccount = matchedAccount;
            StatementMatchedAccountText = $"Matched account: {matchedAccount.Name}";
            await CreatePendingStatementImportAsync(matchedAccount, preview.Statement, filePath);
            return;
        }

        StatementImportAccount = null;
        StatementConnectAccount = Accounts.FirstOrDefault();
        IsStatementAccountUnmatched = true;
        StatementMatchedAccountText = "No saved account matches this statement account number.";
        StatementImportStatusText = "Statement parsed. Choose whether to link it to an existing account or create a new one.";
        SetStatus("Statement account is not saved yet.");
    }

    private async Task CreatePendingStatementImportAsync(
        Account account,
        ParsedStatement? parsedStatement,
        string filePath)
    {
        if (parsedStatement is null || string.IsNullOrWhiteSpace(filePath))
        {
            var resultFromFile = await statementImportService.CreatePendingImportAsync(account.Id, StatementImportPathText.Trim());
            await HandleStatementImportCreateResultAsync(resultFromFile);
            return;
        }

        var result = await statementImportService.CreatePendingImportAsync(
            account.Id,
            filePath,
            parsedStatement);
        await HandleStatementImportCreateResultAsync(result);
    }

    private async Task HandleStatementImportCreateResultAsync(StatementImportCreateResult result)
    {
        StatementImportStatusText = result.Message;

        if (result.Batch is not null)
        {
            IsStatementAccountUnmatched = false;
            await RefreshStatementImportsCoreAsync(result.Batch.Id);
            SetStatus("Statement rows are ready for review.", clearAutomatically: true);
            return;
        }

        SetStatus(result.Message);
    }

    private async Task RefreshAccountsAfterStatementAccountChangeAsync(Guid preferredAccountId)
    {
        Replace(Accounts, await accountRepository.GetAllAsync());
        UpdateSummaries();
        UpdateForecast();
        StatementConnectAccount = Accounts.FirstOrDefault(account => account.Id == preferredAccountId)
            ?? Accounts.FirstOrDefault();
        StatementImportAccount = Accounts.FirstOrDefault(account => account.Id == preferredAccountId);
    }

    private async Task RefreshStatementImportsCoreAsync(Guid? preferredBatchId = null)
    {
        var batches = await statementImportRepository.GetAllBatchesAsync();
        var batchSummaries = batches.Select(batch =>
        {
            var accountName = Accounts.FirstOrDefault(account => account.Id == batch.AccountId)?.Name ?? "Unknown account";
            return new StatementImportBatchSummaryViewModel(batch, accountName);
        });
        Replace(StatementImportBatchSummaries, batchSummaries);
        OnPropertyChanged(nameof(HasStatementImportBatches));

        var selectedBatchId = preferredBatchId
            ?? SelectedStatementImportBatch?.Source.Id
            ?? StatementImportBatchSummaries.FirstOrDefault()?.Source.Id;
        var selectedBatch = StatementImportBatchSummaries.FirstOrDefault(summary => summary.Source.Id == selectedBatchId)
            ?? StatementImportBatchSummaries.FirstOrDefault();

        selectedStatementImportBatch = selectedBatch;
        OnPropertyChanged(nameof(SelectedStatementImportBatch));
        await LoadSelectedStatementRowsAsync();
    }

    private async Task LoadSelectedStatementRowsAsync()
    {
        if (SelectedStatementImportBatch is null)
        {
            StatementImportRows.Clear();
            SelectedStatementImportRow = null;
            OnPropertyChanged(nameof(HasStatementImportRows));
            return;
        }

        try
        {
            var rows = await statementImportRepository.GetRowsByBatchIdAsync(SelectedStatementImportBatch.Source.Id);
            Replace(StatementImportRows, rows.Select(row => new StatementImportRowSummaryViewModel(
                row,
                Categories.FirstOrDefault(category => category.Id == row.SuggestedCategoryId)?.Name,
                Categories.FirstOrDefault(category => category.Id == row.CategoryId)?.Name,
                DefaultCurrency,
                SelectedDateDisplayFormat,
                CreateCategoryChoices(row.Type))));
            SelectedStatementImportRow = StatementImportRows.FirstOrDefault(row =>
                    selectedStatementImportRow is not null && row.Source.Id == selectedStatementImportRow.Source.Id)
                ?? StatementImportRows.FirstOrDefault(row => row.Source.Status == StatementImportRowStatus.Pending)
                ?? StatementImportRows.FirstOrDefault();
            OnPropertyChanged(nameof(HasStatementImportRows));
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message);
        }
    }

    private async Task DeleteAllDataAsync()
    {
        if (!DeleteAllBackupAcknowledged)
        {
            SetStatus("Confirm that you have exported a backup or intentionally do not want one.");
            return;
        }

        if (!string.Equals(DeleteAllConfirmationText.Trim(), "DELETE ALL DATA", StringComparison.Ordinal))
        {
            SetStatus("Type DELETE ALL DATA to confirm the local reset.");
            return;
        }

        await RunBusyAsync(async () =>
        {
            await DeleteAllRepositoryDataAsync();
            await settingsRepository.SaveAsync(CreateDefaultSettings());
            DeleteAllConfirmationText = string.Empty;
            DeleteAllBackupAcknowledged = false;
            latestReconciliationResult = null;
            CheckInExpectedTransactions.Clear();
            OnPropertyChanged(nameof(HasCheckInExpectedTransactions));
            await RefreshAsync();
            SetStatus("All local financial data has been deleted.", clearAutomatically: true);
        });
    }

    private async Task SavePreferencesAsync()
    {
        var settings = new AppSettings(
            NormalizeCurrency(DefaultCurrency),
            SelectedForecastPeriod,
            SelectedReminderFrequency,
            SelectedDateDisplayFormat);

        await settingsRepository.SaveAsync(settings);
        DefaultCurrency = settings.DefaultCurrency;
        NewAccountCurrency = settings.DefaultCurrency;
        UpdateForecast();
        SetStatus("Preferences saved.", clearAutomatically: true);
    }

    private async Task RefreshAfterMutationAsync(string message)
    {
        await RefreshAsync();
        SetStatus(message, clearAutomatically: true);
    }

    private async Task SaveAppliedTransactionAsync(Transaction transaction)
    {
        var account = await accountRepository.GetByIdAsync(transaction.AccountId);
        if (account is null)
        {
            throw new InvalidOperationException("The selected account could not be found.");
        }

        await accountRepository.SaveAsync(transactionBalanceService.Apply(account, transaction));
        await ApplyTransferDestinationAsync(transaction);
        await transactionRepository.SaveAsync(transaction);
    }

    private async Task ReverseAppliedTransactionAsync(Transaction transaction)
    {
        var account = await accountRepository.GetByIdAsync(transaction.AccountId);
        if (account is not null)
        {
            await accountRepository.SaveAsync(transactionBalanceService.Reverse(account, transaction));
        }

        await ReverseTransferDestinationAsync(transaction);
    }

    private async Task ApplyTransferDestinationAsync(Transaction transaction)
    {
        if (transaction.Type != TransactionType.Transfer)
        {
            return;
        }

        if (transaction.DestinationAccountId is { } destinationAccountId)
        {
            var destinationAccount = await accountRepository.GetByIdAsync(destinationAccountId);
            if (destinationAccount is not null)
            {
                await accountRepository.SaveAsync(destinationAccount with
                {
                    CurrentBalance = destinationAccount.CurrentBalance + Math.Abs(transaction.Amount)
                });
            }
        }

        if (transaction.DestinationGoalId is { } destinationGoalId)
        {
            var destinationGoal = await savingsGoalRepository.GetByIdAsync(destinationGoalId);
            if (destinationGoal is not null)
            {
                await savingsGoalRepository.SaveAsync(destinationGoal with
                {
                    CurrentAmount = destinationGoal.CurrentAmount + Math.Abs(transaction.Amount)
                });
            }
        }
    }

    private async Task ReverseTransferDestinationAsync(Transaction transaction)
    {
        if (transaction.Type != TransactionType.Transfer)
        {
            return;
        }

        if (transaction.DestinationAccountId is { } destinationAccountId)
        {
            var destinationAccount = await accountRepository.GetByIdAsync(destinationAccountId);
            if (destinationAccount is not null)
            {
                await accountRepository.SaveAsync(destinationAccount with
                {
                    CurrentBalance = destinationAccount.CurrentBalance - Math.Abs(transaction.Amount)
                });
            }
        }

        if (transaction.DestinationGoalId is { } destinationGoalId)
        {
            var destinationGoal = await savingsGoalRepository.GetByIdAsync(destinationGoalId);
            if (destinationGoal is not null)
            {
                await savingsGoalRepository.SaveAsync(destinationGoal with
                {
                    CurrentAmount = Math.Max(0m, destinationGoal.CurrentAmount - Math.Abs(transaction.Amount))
                });
            }
        }
    }

    private async Task AdvanceScheduledTransactionAsync(Guid scheduledTransactionId, DateOnly completedDate)
    {
        var scheduledTransaction = await scheduledTransactionRepository.GetByIdAsync(scheduledTransactionId);
        if (scheduledTransaction is null)
        {
            return;
        }

        var nextOccurrence = recurrenceService.GetNextOccurrence(scheduledTransaction.RecurrenceRule, completedDate);
        await scheduledTransactionRepository.SaveAsync(scheduledTransaction with
        {
            NextOccurrence = nextOccurrence ?? completedDate,
            Active = nextOccurrence is not null
        });
    }

    private ForecastResult CreateForecastThrough(DateOnly date)
    {
        var startDate = dateProvider.Today;
        var endDate = date >= startDate ? date : startDate;

        return forecastService.CreateForecast(new ForecastRequest(
            startDate,
            endDate,
            Accounts.ToArray(),
            ScheduledTransactions.ToArray(),
            SavingsGoals.ToArray()));
    }

    private async Task DeleteAllRepositoryDataAsync()
    {
        await statementImportRepository.DeleteAllAsync();
        await categoryLearningRuleRepository.DeleteAllAsync();
        await transactionRepository.DeleteAllAsync();
        await scheduledTransactionRepository.DeleteAllAsync();
        await savingsGoalRepository.DeleteAllAsync();
        await categoryRepository.DeleteAllAsync();
        await accountRepository.DeleteAllAsync();
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings(
            "EUR",
            ForecastPeriod.ThirtyDays,
            ReminderFrequency.Weekly,
            DateDisplayFormat.DayMonthYear);
    }

    private static string CreateDefaultBackupPath()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var directory = string.IsNullOrWhiteSpace(documents)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : documents;

        return Path.Combine(directory, $"banccoon_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
    }

    private static string FormatImportResult(ImportResult result)
    {
        if (!result.Validation.IsValid)
        {
            return $"Import failed validation: {string.Join(" ", result.Validation.Errors)}";
        }

        if (result.Mode == ImportMode.ValidateOnly)
        {
            return "Import file is valid. No local data was changed.";
        }

        return $"{DisplayText.Format(result.Mode)} import completed: {result.AccountsImported} account(s), {result.TransactionsImported} transaction(s), {result.ScheduledTransactionsImported} scheduled item(s), {result.CategoriesImported} categor(ies), and {result.SavingsGoalsImported} goal(s).";
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        try
        {
            IsBusy = true;
            await action();
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetStatus(string message, bool clearAutomatically = false)
    {
        statusClearCancellation?.Cancel();
        StatusMessage = message;

        if (!clearAutomatically)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        statusClearCancellation = cancellation;
        _ = ClearStatusAfterDelayAsync(cancellation.Token);
    }

    private async Task ClearStatusAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                StatusMessage = string.Empty;
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private CreditCardDetails? CreateCreditCardDetailsForNewAccount()
    {
        if (SelectedAccountType != AccountType.CreditCard)
        {
            return null;
        }

        return new CreditCardDetails(
            ReadOptionalDecimal(NewCreditCardDebtText),
            StatementDayOfMonth: null,
            ReadOptionalPaymentDueDay(NewCreditCardPaymentDueDayText),
            ReadOptionalDecimal(NewCreditCardMinimumPaymentText),
            ReadOptionalDecimal(NewCreditCardPlannedPaymentText));
    }

    private CreditCardDetails? CreateCreditCardDetailsForSelectedAccount()
    {
        if (SelectedAccountEditType != AccountType.CreditCard)
        {
            return null;
        }

        return new CreditCardDetails(
            ReadOptionalDecimal(SelectedAccountDebtText),
            SelectedAccount?.CreditCardDetails?.StatementDayOfMonth,
            ReadOptionalPaymentDueDay(SelectedAccountPaymentDueDayText),
            ReadOptionalDecimal(SelectedAccountMinimumPaymentText),
            ReadOptionalDecimal(SelectedAccountPlannedPaymentText));
    }

    private void LoadTransactionEditor(Transaction? transaction)
    {
        if (transaction is null)
        {
            EditTransactionAccount = Accounts.FirstOrDefault();
            EditTransactionType = TransactionType.Expense;
            EditTransactionDate = dateProvider.Today.ToDateTime(TimeOnly.MinValue);
            EditTransactionAmountText = string.Empty;
            EditTransactionNotes = string.Empty;
            EditTransactionCategoryName = string.Empty;
            EditTransferDestinationKind = TransferDestinationKind.Account;
            EditTransferDestinationAccount = Accounts.FirstOrDefault();
            EditTransferDestinationGoal = SavingsGoals.FirstOrDefault();
            EditTransactionCategory = EditTransactionCategoryChoices.FirstOrDefault();
            return;
        }

        EditTransactionAccount = Accounts.FirstOrDefault(account => account.Id == transaction.AccountId);
        EditTransactionType = transaction.Type;
        EditTransactionDate = transaction.Date.ToDateTime(TimeOnly.MinValue);
        EditTransactionAmountText = ToInputText(transaction.Amount);
        EditTransactionNotes = transaction.Notes ?? string.Empty;
        EditTransactionCategoryName = string.Empty;
        EditTransferDestinationKind = transaction.DestinationGoalId.HasValue
            ? TransferDestinationKind.Goal
            : TransferDestinationKind.Account;
        EditTransferDestinationAccount = Accounts.FirstOrDefault(account => account.Id == transaction.DestinationAccountId);
        EditTransferDestinationGoal = SavingsGoals.FirstOrDefault(goal => goal.Id == transaction.DestinationGoalId);
        UpdateCategoryChoices();
        EditTransactionCategory = EditTransactionCategoryChoices.FirstOrDefault(choice => choice.CategoryId == transaction.CategoryId)
            ?? EditTransactionCategoryChoices.FirstOrDefault();
    }

    private void LoadScheduledEditor(ScheduledTransaction? scheduledTransaction)
    {
        if (scheduledTransaction is null)
        {
            EditScheduledAccount = Accounts.FirstOrDefault();
            EditScheduledType = TransactionType.Expense;
            EditScheduledDate = dateProvider.Today.ToDateTime(TimeOnly.MinValue);
            EditScheduledName = string.Empty;
            EditScheduledAmountText = string.Empty;
            EditScheduledNotes = string.Empty;
            EditScheduledIntervalText = "1";
            EditScheduledFrequency = RecurrenceFrequency.Monthly;
            EditScheduledCategoryName = string.Empty;
            EditScheduledCategory = EditScheduledCategoryChoices.FirstOrDefault();
            return;
        }

        EditScheduledAccount = Accounts.FirstOrDefault(account => account.Id == scheduledTransaction.AccountId);
        EditScheduledType = scheduledTransaction.Type;
        EditScheduledDate = scheduledTransaction.NextOccurrence.ToDateTime(TimeOnly.MinValue);
        EditScheduledName = scheduledTransaction.Name;
        EditScheduledAmountText = ToInputText(scheduledTransaction.Amount);
        EditScheduledNotes = scheduledTransaction.Notes ?? string.Empty;
        EditScheduledIntervalText = scheduledTransaction.RecurrenceRule.Interval.ToString(CultureInfo.InvariantCulture);
        EditScheduledFrequency = scheduledTransaction.RecurrenceRule.Frequency;
        EditScheduledCategoryName = string.Empty;
        UpdateCategoryChoices();
        EditScheduledCategory = EditScheduledCategoryChoices.FirstOrDefault(choice => choice.CategoryId == scheduledTransaction.CategoryId)
            ?? EditScheduledCategoryChoices.FirstOrDefault();
    }

    private void LoadGoalEditor(SavingsGoal? goal)
    {
        if (goal is null)
        {
            EditGoalAccount = Accounts.FirstOrDefault(account => account.Type == AccountType.Savings) ?? Accounts.FirstOrDefault();
            EditGoalName = string.Empty;
            EditGoalTargetAmountText = string.Empty;
            EditGoalCurrentAmountText = string.Empty;
            EditGoalTargetDate = dateProvider.Today.ToDateTime(TimeOnly.MinValue).AddMonths(6);
            return;
        }

        EditGoalAccount = Accounts.FirstOrDefault(account => account.Id == goal.AccountId);
        EditGoalName = goal.Name;
        EditGoalTargetAmountText = ToInputText(goal.TargetAmount);
        EditGoalCurrentAmountText = ToInputText(goal.CurrentAmount);
        EditGoalTargetDate = (goal.TargetDate ?? dateProvider.Today).ToDateTime(TimeOnly.MinValue);
    }

    private void LoadSelectedAccountEditor(Account? account)
    {
        if (account is null)
        {
            SelectedAccountName = string.Empty;
            SelectedAccountCurrency = DefaultCurrency;
            SelectedAccountBalanceText = string.Empty;
            SelectedAccountDebtText = string.Empty;
            SelectedAccountMinimumPaymentText = string.Empty;
            SelectedAccountPlannedPaymentText = string.Empty;
            SelectedAccountPaymentDueDayText = string.Empty;
            SelectedAccountPayoffPaymentText = string.Empty;
            SelectedAccountNumber = string.Empty;
            SelectedCardLastFourDigits = string.Empty;
            SelectedAccountIncludeInDashboardTotals = true;
            return;
        }

        SelectedAccountName = account.Name;
        SelectedAccountEditType = account.Type;
        SelectedAccountCurrency = account.Currency;
        SelectedAccountBalanceText = ToInputText(account.CurrentBalance);
        SelectedAccountNumber = account.AccountNumber ?? string.Empty;
        SelectedCardLastFourDigits = account.CardLastFourDigits ?? string.Empty;
        SelectedAccountIncludeInDashboardTotals = account.IncludeInDashboardTotals;
        SelectedAccountDebtText = ToInputText(account.CreditCardDetails?.CurrentDebt);
        SelectedAccountMinimumPaymentText = ToInputText(account.CreditCardDetails?.MinimumPayment);
        SelectedAccountPlannedPaymentText = ToInputText(account.CreditCardDetails?.PlannedPaymentAmount);
        SelectedAccountPaymentDueDayText = account.CreditCardDetails?.PaymentDueDayOfMonth?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        SelectedAccountPayoffPaymentText = ToInputText(
            account.CreditCardDetails?.PlannedPaymentAmount
            ?? account.CreditCardDetails?.MinimumPayment);
    }

    private void RecalculateSelectedAccountPayoff()
    {
        if (SelectedAccount?.Type != AccountType.CreditCard)
        {
            SelectedAccountPayoffSummary = "Select a credit card to calculate payoff timing.";
            return;
        }

        if (!TryReadDecimal(SelectedAccountPayoffPaymentText, out var paymentAmount) || paymentAmount <= 0m)
        {
            SelectedAccountPayoffSummary = "Enter a monthly payment amount to calculate payoff timing.";
            return;
        }

        var accountForCalculation = SelectedAccount with
        {
            CreditCardDetails = CreateCreditCardDetailsForSelectedAccount()
        };
        var manualFinanceCharge = ReadOptionalDecimal(SelectedAccountManualFinanceChargeText) ?? 0m;
        var firstPaymentDate = GetNextPaymentDate(accountForCalculation, dateProvider.Today);
        var plan = creditCardForecastService.CalculatePayoffPlan(
            accountForCalculation,
            paymentAmount,
            firstPaymentDate,
            manualFinanceCharge);

        if (!plan.IsPaidOff)
        {
            SelectedAccountPayoffSummary = $"Not paid off within {plan.MonthCount} months at {FormatMoney(paymentAmount, accountForCalculation.Currency)} per month.";
            return;
        }

        var finalPaymentDate = plan.FinalPaymentDate is null
            ? "the final payment"
            : DateDisplay.Format(plan.FinalPaymentDate.Value, SelectedDateDisplayFormat);
        SelectedAccountPayoffSummary = plan.MonthCount == 0
            ? "No outstanding card debt."
            : $"Paid off by {finalPaymentDate} after {plan.MonthCount} payments. Total paid: {FormatMoney(plan.TotalPaid, accountForCalculation.Currency)}.";
    }

    private void EnsureDefaultSelections()
    {
        SelectedAccount = FindFreshAccount(SelectedAccount) ?? Accounts.FirstOrDefault();
        NewTransactionAccount = FindFreshAccount(NewTransactionAccount) ?? Accounts.FirstOrDefault();
        NewTransferDestinationAccount = FindFreshAccount(NewTransferDestinationAccount)
            ?? Accounts.FirstOrDefault(account => account.Id != NewTransactionAccount?.Id)
            ?? Accounts.FirstOrDefault();
        NewTransferDestinationGoal = FindFreshGoal(NewTransferDestinationGoal) ?? SavingsGoals.FirstOrDefault();
        EditTransactionAccount = FindFreshAccount(EditTransactionAccount) ?? Accounts.FirstOrDefault();
        EditTransferDestinationAccount = FindFreshAccount(EditTransferDestinationAccount)
            ?? Accounts.FirstOrDefault(account => account.Id != EditTransactionAccount?.Id)
            ?? Accounts.FirstOrDefault();
        EditTransferDestinationGoal = FindFreshGoal(EditTransferDestinationGoal) ?? SavingsGoals.FirstOrDefault();
        NewScheduledAccount = FindFreshAccount(NewScheduledAccount) ?? Accounts.FirstOrDefault();
        EditScheduledAccount = FindFreshAccount(EditScheduledAccount) ?? Accounts.FirstOrDefault();
        NewGoalAccount = FindFreshAccount(NewGoalAccount)
            ?? Accounts.FirstOrDefault(account => account.Type == AccountType.Savings)
            ?? Accounts.FirstOrDefault();
        EditGoalAccount = FindFreshAccount(EditGoalAccount)
            ?? Accounts.FirstOrDefault(account => account.Type == AccountType.Savings)
            ?? Accounts.FirstOrDefault();
        ReconciliationAccount = FindFreshAccount(ReconciliationAccount) ?? Accounts.FirstOrDefault();
        StatementImportAccount = FindFreshAccount(StatementImportAccount);
        StatementConnectAccount = FindFreshAccount(StatementConnectAccount) ?? Accounts.FirstOrDefault();
        SelectedTransactionCategory = FindFreshCategoryChoice(SelectedTransactionCategory, TransactionCategoryChoices)
            ?? TransactionCategoryChoices.FirstOrDefault();
        EditTransactionCategory = FindFreshCategoryChoice(EditTransactionCategory, EditTransactionCategoryChoices)
            ?? EditTransactionCategoryChoices.FirstOrDefault();
        SelectedScheduledCategory = FindFreshCategoryChoice(SelectedScheduledCategory, ScheduledCategoryChoices)
            ?? ScheduledCategoryChoices.FirstOrDefault();
        EditScheduledCategory = FindFreshCategoryChoice(EditScheduledCategory, EditScheduledCategoryChoices)
            ?? EditScheduledCategoryChoices.FirstOrDefault();
        SelectedGroupedSpendingCategory = FindFreshCategoryChoice(SelectedGroupedSpendingCategory, GroupedSpendingCategoryChoices)
            ?? GroupedSpendingCategoryChoices.FirstOrDefault();
        SelectedStatementImportCategory = FindFreshCategoryChoice(SelectedStatementImportCategory, StatementImportCategoryChoices)
            ?? StatementImportCategoryChoices.FirstOrDefault();
    }

    private Account? FindFreshAccount(Account? account)
    {
        return account is null
            ? null
            : Accounts.FirstOrDefault(candidate => candidate.Id == account.Id);
    }

    private SavingsGoal? FindFreshGoal(SavingsGoal? goal)
    {
        return goal is null
            ? null
            : SavingsGoals.FirstOrDefault(candidate => candidate.Id == goal.Id);
    }

    private CategoryChoiceViewModel? FindFreshCategoryChoice(
        CategoryChoiceViewModel? categoryChoice,
        IEnumerable<CategoryChoiceViewModel> choices)
    {
        return categoryChoice is null
            ? null
            : choices.FirstOrDefault(candidate =>
                candidate.CategoryId == categoryChoice.CategoryId
                && string.Equals(candidate.Name, categoryChoice.Name, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateCategoryChoices()
    {
        Replace(TransactionCategoryChoices, CreateCategoryChoices(SelectedTransactionType));
        Replace(EditTransactionCategoryChoices, CreateCategoryChoices(EditTransactionType));
        Replace(ScheduledCategoryChoices, CreateCategoryChoices(SelectedScheduledType));
        Replace(EditScheduledCategoryChoices, CreateCategoryChoices(EditScheduledType));
        Replace(GroupedSpendingCategoryChoices, CreateCategoryChoices(SelectedReconciliationTransactionType));
        UpdateStatementImportCategoryChoices();
    }

    private void UpdateStatementImportCategoryChoices()
    {
        var type = SelectedStatementImportRow?.Source.Type ?? TransactionType.Expense;
        Replace(StatementImportCategoryChoices, CreateCategoryChoices(type));
        SelectedStatementImportCategory = SelectedStatementImportRow?.Source.SuggestedCategoryId is { } suggestedCategoryId
            ? StatementImportCategoryChoices.FirstOrDefault(choice => choice.CategoryId == suggestedCategoryId)
                ?? StatementImportCategoryChoices.FirstOrDefault()
            : StatementImportCategoryChoices.FirstOrDefault();
    }

    private IEnumerable<CategoryChoiceViewModel> CreateCategoryChoices(TransactionType type)
    {
        var savedCategories = Categories
            .Where(category => category.Type is null || category.Type == type)
            .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingNames = savedCategories
            .Select(category => category.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new[]
            {
                CategoryChoiceViewModel.None
            }
            .Concat(savedCategories.Select(category => new CategoryChoiceViewModel(category.Id, category.Name, type)))
            .Concat(DefaultCategories
                .Where(defaultCategory => defaultCategory.Type == type && !existingNames.Contains(defaultCategory.Name))
                .Select(defaultCategory => CategoryChoiceViewModel.Suggestion(defaultCategory.Name, defaultCategory.Type)));
    }

    private void UpdateSummaries()
    {
        Replace(AccountSummaries, Accounts.Select(account => new AccountSummaryViewModel(account)));
        Replace(CategorySummaries, Categories.Select(category => new CategorySummaryViewModel(category)));
        Replace(TransactionSummaries, Transactions
            .OrderByDescending(transaction => transaction.Date)
            .Select(transaction => new TransactionSummaryViewModel(
            transaction,
            Accounts.FirstOrDefault(account => account.Id == transaction.AccountId)?.Name ?? "Unknown account",
            Accounts.FirstOrDefault(account => account.Id == transaction.DestinationAccountId)?.Name,
            SavingsGoals.FirstOrDefault(goal => goal.Id == transaction.DestinationGoalId)?.Name,
            Categories.FirstOrDefault(category => category.Id == transaction.CategoryId)?.Name,
            DefaultCurrency,
            SelectedDateDisplayFormat)));
        Replace(ScheduledTransactionSummaries, ScheduledTransactions.Select(item => new ScheduledTransactionSummaryViewModel(
            item,
            Accounts.FirstOrDefault(account => account.Id == item.AccountId)?.Name ?? "Unknown account",
            Categories.FirstOrDefault(category => category.Id == item.CategoryId)?.Name,
            DefaultCurrency,
            SelectedDateDisplayFormat)));
        Replace(SavingsGoalSummaries, SavingsGoals.Select(goal => new SavingsGoalSummaryViewModel(
            goal,
            Accounts.FirstOrDefault(account => account.Id == goal.AccountId)?.Name,
            DefaultCurrency,
            SelectedDateDisplayFormat)));

        OnPropertyChanged(nameof(HasAccounts));
        OnPropertyChanged(nameof(HasCategories));
        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(HasScheduledTransactions));
        OnPropertyChanged(nameof(HasSavingsGoals));
    }

    private void UpdateForecast()
    {
        ForecastPeriodText = GetForecastPeriodLabel(SelectedForecastPeriod);

        if (Accounts.Count == 0)
        {
            CurrentBalanceText = FormatMoney(0m, DefaultCurrency);
            AvailableToSpendText = FormatMoney(0m, DefaultCurrency);
            LowestForecastText = FormatMoney(0m, DefaultCurrency);
            UpcomingObligationsText = FormatMoney(0m, DefaultCurrency);
            DashboardCurrentBalanceText = FormatMoney(0m, DefaultCurrency);
            DashboardAvailableToSpendText = FormatMoney(0m, DefaultCurrency);
            DashboardLowestForecastText = FormatMoney(0m, DefaultCurrency);
            DashboardUpcomingObligationsText = FormatMoney(0m, DefaultCurrency);
            DashboardIncludedAccountsText = "No accounts included in dashboard totals yet.";
            ForecastEvents.Clear();
            UpcomingObligations.Clear();
            DashboardForecastPoints.Clear();
            SelectedDashboardForecastPoint = null;
            OnPropertyChanged(nameof(HasForecastEvents));
            OnPropertyChanged(nameof(HasUpcomingObligations));
            OnPropertyChanged(nameof(HasDashboardForecastPoints));
            return;
        }

        var startDate = dateProvider.Today;
        var endDate = startDate.AddDays((int)SelectedForecastPeriod - 1);
        var forecast = forecastService.CreateForecast(new ForecastRequest(
            startDate,
            endDate,
            Accounts.ToArray(),
            ScheduledTransactions.ToArray(),
            SavingsGoals.ToArray()));

        CurrentBalanceText = FormatMoney(forecast.CurrentBalance, DefaultCurrency);
        AvailableToSpendText = FormatMoney(forecast.AvailableToSpend, DefaultCurrency);
        LowestForecastText = FormatMoney(forecast.LowestForecastedBalance, DefaultCurrency);
        UpcomingObligationsText = FormatMoney(forecast.UpcomingObligations.Sum(obligation => obligation.Amount), DefaultCurrency);

        Replace(ForecastEvents, forecast.Events.Select(forecastEvent => new ForecastEventSummaryViewModel(
            forecastEvent,
            DefaultCurrency,
            SelectedDateDisplayFormat)));

        var dashboardAccounts = Accounts
            .Where(account => !account.IsArchived && account.IncludeInDashboardTotals)
            .ToArray();
        var activeAccountsCount = Accounts.Count(account => !account.IsArchived);
        DashboardIncludedAccountsText = $"{dashboardAccounts.Length} of {activeAccountsCount} account(s) included in dashboard totals.";

        if (dashboardAccounts.Length == 0)
        {
            DashboardCurrentBalanceText = FormatMoney(0m, DefaultCurrency);
            DashboardAvailableToSpendText = FormatMoney(0m, DefaultCurrency);
            DashboardLowestForecastText = FormatMoney(0m, DefaultCurrency);
            DashboardUpcomingObligationsText = FormatMoney(0m, DefaultCurrency);
            UpcomingObligations.Clear();
            DashboardForecastPoints.Clear();
            SelectedDashboardForecastPoint = null;
            OnPropertyChanged(nameof(HasForecastEvents));
            OnPropertyChanged(nameof(HasUpcomingObligations));
            OnPropertyChanged(nameof(HasDashboardForecastPoints));
            return;
        }

        var dashboardAccountIds = dashboardAccounts
            .Select(account => account.Id)
            .ToHashSet();
        var dashboardScheduledTransactions = ScheduledTransactions
            .Where(scheduledTransaction => dashboardAccountIds.Contains(scheduledTransaction.AccountId))
            .ToArray();
        var dashboardSavingsGoals = SavingsGoals
            .Where(goal => goal.AccountId is null || dashboardAccountIds.Contains(goal.AccountId.Value))
            .ToArray();
        var dashboardForecast = forecastService.CreateForecast(new ForecastRequest(
            startDate,
            endDate,
            dashboardAccounts,
            dashboardScheduledTransactions,
            dashboardSavingsGoals));

        DashboardCurrentBalanceText = FormatMoney(dashboardForecast.CurrentBalance, DefaultCurrency);
        DashboardAvailableToSpendText = FormatMoney(dashboardForecast.AvailableToSpend, DefaultCurrency);
        DashboardLowestForecastText = FormatMoney(dashboardForecast.LowestForecastedBalance, DefaultCurrency);
        DashboardUpcomingObligationsText = FormatMoney(dashboardForecast.UpcomingObligations.Sum(obligation => obligation.Amount), DefaultCurrency);

        Replace(UpcomingObligations, dashboardForecast.UpcomingObligations.Select(obligation => new UpcomingObligationSummaryViewModel(
            obligation,
            DefaultCurrency,
            SelectedDateDisplayFormat)));
        var selectedDate = SelectedDashboardForecastPoint?.Date;
        var chartPoints = CreateForecastChartPoints(dashboardForecast, DefaultCurrency, SelectedDateDisplayFormat);
        Replace(DashboardForecastPoints, chartPoints);
        SelectedDashboardForecastPoint = selectedDate is null
            ? SelectDefaultChartPoint(DashboardForecastPoints)
            : DashboardForecastPoints.FirstOrDefault(point => point.Date == selectedDate.Value)
                ?? SelectDefaultChartPoint(DashboardForecastPoints);
        OnPropertyChanged(nameof(HasForecastEvents));
        OnPropertyChanged(nameof(HasUpcomingObligations));
        OnPropertyChanged(nameof(HasDashboardForecastPoints));
    }

    private static IReadOnlyList<ForecastChartPointViewModel> CreateForecastChartPoints(
        ForecastResult forecast,
        string currency,
        DateDisplayFormat dateDisplayFormat)
    {
        var eventsByDate = forecast.Events
            .GroupBy(forecastEvent => forecastEvent.Date)
            .ToDictionary(group => group.Key, group => group
                .OrderBy(forecastEvent => forecastEvent.SignedAmount)
                .ThenBy(forecastEvent => forecastEvent.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        var points = new List<ForecastChartPointViewModel>();
        var runningBalance = forecast.CurrentBalance;
        var totalDays = forecast.EndDate.DayNumber - forecast.StartDate.DayNumber;

        for (var offset = 0; offset <= totalDays; offset++)
        {
            var date = forecast.StartDate.AddDays(offset);
            var dayEvents = eventsByDate.TryGetValue(date, out var eventsForDate)
                ? eventsForDate
                : Array.Empty<ForecastEvent>();

            foreach (var dayEvent in dayEvents)
            {
                runningBalance += dayEvent.SignedAmount;
            }

            points.Add(new ForecastChartPointViewModel(
                date,
                runningBalance,
                dayEvents.Select(dayEvent => $"{dayEvent.Name}: {FormatMoney(dayEvent.SignedAmount, currency)}").ToArray(),
                currency,
                dateDisplayFormat));
        }

        return points;
    }

    private static ForecastChartPointViewModel? SelectDefaultChartPoint(IEnumerable<ForecastChartPointViewModel> points)
    {
        return points
            .OrderBy(point => point.Balance)
            .ThenBy(point => point.Date)
            .FirstOrDefault();
    }

    private void UpdateStatementParserStatus()
    {
        if (string.IsNullOrWhiteSpace(StatementImportPathText))
        {
            StatementImportStatusText = "No statement selected.";
            return;
        }

        var parser = statementParserRegistry.FindParser(new StatementParseRequest(StatementImportPathText.Trim()));
        StatementImportStatusText = parser is null
            ? "No parser is available for this statement yet."
            : $"{parser.Descriptor.Name} can read this statement.";
    }

    private void ResetPendingStatementResolution(string filePath)
    {
        pendingStatementPreview = null;
        pendingStatementFilePath = filePath;
        StatementImportPathText = filePath;
        StatementImportAccount = null;
        IsStatementAccountUnmatched = false;
        StatementDetectedAccountText = "No statement account detected yet.";
        StatementDetectedCardText = string.Empty;
        StatementMatchedAccountText = "No account matched yet.";
    }

    private void ClearPendingStatementResolution()
    {
        pendingStatementPreview = null;
        pendingStatementFilePath = string.Empty;
        StatementImportPathText = string.Empty;
        StatementImportAccount = null;
        StatementConnectAccount = Accounts.FirstOrDefault();
        IsStatementAccountUnmatched = false;
        StatementSelectedFileText = "No file selected.";
        StatementDetectedAccountText = "No statement account detected yet.";
        StatementDetectedCardText = string.Empty;
        StatementMatchedAccountText = "No account matched yet.";
        StatementImportStatusText = "No statement selected.";
    }

    private Account? FindAccountByAccountNumber(string? accountNumber)
    {
        var normalized = NormalizeAccountNumber(accountNumber);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return Accounts.FirstOrDefault(account =>
            string.Equals(
                NormalizeAccountNumber(account.AccountNumber),
                normalized,
                StringComparison.Ordinal));
    }

    private static string FormatDetectedAccountText(string? accountNumber)
    {
        var normalized = NormalizeAccountNumber(accountNumber);
        return string.IsNullOrWhiteSpace(normalized)
            ? "Account number was not found in the statement."
            : $"Statement account {FormatAccountNumber(normalized)}";
    }

    private static string FormatDetectedCardText(string? cardLastFourDigits)
    {
        return string.IsNullOrWhiteSpace(cardLastFourDigits)
            ? "Card ending was not found in the statement."
            : $"Card ending {cardLastFourDigits}";
    }

    private static string CreateDefaultStatementAccountName(ParsedStatement statement)
    {
        return string.IsNullOrWhiteSpace(statement.CardLastFourDigits)
            ? "Imported statement account"
            : $"Sberbank card {statement.CardLastFourDigits}";
    }

    private static string FormatAccountNumber(string? value)
    {
        var normalized = NormalizeAccountNumber(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            Enumerable.Range(0, (normalized.Length + 3) / 4)
                .Select(index =>
                {
                    var start = index * 4;
                    var length = Math.Min(4, normalized.Length - start);
                    return normalized.Substring(start, length);
                }));
    }

    private static string NormalizeAccountNumber(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Concat(value.Where(char.IsDigit));
    }

    private static RecurrenceRule CreateRecurrenceRule(
        RecurrenceFrequency frequency,
        int interval,
        DateOnly startDate)
    {
        return frequency switch
        {
            RecurrenceFrequency.Weekly => new RecurrenceRule(
                frequency,
                interval,
                startDate,
                DayOfWeek: startDate.DayOfWeek),
            RecurrenceFrequency.Monthly => new RecurrenceRule(
                frequency,
                interval,
                startDate,
                DayOfMonth: startDate.Day,
                MonthlyMode: MonthlyRecurrenceMode.DayOfMonth),
            _ => new RecurrenceRule(frequency, interval, startDate)
        };
    }

    private static DateOnly GetNextPaymentDate(Account account, DateOnly today)
    {
        var dueDay = account.CreditCardDetails?.PaymentDueDayOfMonth;
        if (dueDay is null)
        {
            return today;
        }

        var clampedDay = Math.Min(dueDay.Value, DateTime.DaysInMonth(today.Year, today.Month));
        var date = new DateOnly(today.Year, today.Month, clampedDay);
        if (date < today)
        {
            var nextMonth = today.AddMonths(1);
            clampedDay = Math.Min(dueDay.Value, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
            date = new DateOnly(nextMonth.Year, nextMonth.Month, clampedDay);
        }

        return date;
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
        {
            collection.Add(value);
        }
    }

    private static bool TryReadDecimal(string text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadInt(string text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
            || int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static decimal? ReadOptionalDecimal(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? null
            : TryReadDecimal(text, out var value) ? Math.Max(0m, value) : null;
    }

    private static int? ReadOptionalPaymentDueDay(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !TryReadInt(text, out var day))
        {
            return null;
        }

        return day is >= 1 and <= 31 ? day : null;
    }

    private static string FormatMoney(decimal amount, string currency)
    {
        return $"{NormalizeCurrency(currency)} {amount:N2}";
    }

    private static string ToInputText(decimal? value)
    {
        return value?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string? CleanOptionalText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static bool IsCardAccountType(AccountType accountType)
    {
        return accountType is AccountType.DebitCard or AccountType.CreditCard;
    }

    private static bool TryNormalizeCardLastFour(string text, out string? cardLastFourDigits)
    {
        cardLastFourDigits = CleanOptionalText(text);
        if (cardLastFourDigits is null)
        {
            return true;
        }

        return cardLastFourDigits.Length == 4
            && cardLastFourDigits.All(char.IsDigit);
    }

    private static string NormalizeCurrency(string? currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "EUR"
            : currency.Trim().ToUpperInvariant();
    }

    private static string GetForecastPeriodLabel(ForecastPeriod period)
    {
        return $"{(int)period} days";
    }
}

public sealed class ForecastChartPointViewModel
{
    public ForecastChartPointViewModel(
        DateOnly date,
        decimal balance,
        IReadOnlyList<string> eventSummaries,
        string currency,
        DateDisplayFormat dateDisplayFormat)
    {
        Date = date;
        Balance = balance;
        EventSummaries = eventSummaries;
        DateText = DateDisplay.Format(date, dateDisplayFormat);
        ShortDateText = DateDisplay.FormatShortWithoutYear(date, dateDisplayFormat);
        BalanceText = $"{currency} {balance:N2}";
        EventsText = eventSummaries.Count == 0
            ? "No planned events"
            : string.Join(" | ", eventSummaries);
    }

    public DateOnly Date { get; }

    public decimal Balance { get; }

    public IReadOnlyList<string> EventSummaries { get; }

    public string DateText { get; }

    public string ShortDateText { get; }

    public string BalanceText { get; }

    public string EventsText { get; }
}

public sealed class ExpectedTransactionReviewViewModel : ViewModelBase
{
    private ExpectedTransactionReview source;
    private string decisionText;
    private string editableAmountText;

    public ExpectedTransactionReviewViewModel(
        ExpectedTransactionReview source,
        string accountName,
        string? categoryName,
        string currency,
        DateDisplayFormat dateDisplayFormat)
    {
        this.source = source;
        AccountName = accountName;
        CategoryText = categoryName ?? "None";
        AmountText = $"{currency} {source.ExpectedEvent.Amount:N2}";
        editableAmountText = source.ExpectedEvent.Amount.ToString("0.##", CultureInfo.InvariantCulture);
        DateText = DateDisplay.Format(source.ExpectedEvent.Date, dateDisplayFormat);
        TypeText = DisplayText.Format(source.ExpectedEvent.Type);
        decisionText = DisplayText.Format(source.Decision);
    }

    public ExpectedTransactionReview Source => source;

    public string Name => source.ExpectedEvent.Name;

    public string AccountName { get; }

    public string CategoryText { get; }

    public string AmountText { get; }

    public string EditableAmountText
    {
        get => editableAmountText;
        set => SetProperty(ref editableAmountText, value);
    }

    public string DateText { get; }

    public string TypeText { get; }

    public string DetailText => $"{DateText} - {AccountName} - {TypeText} - {AmountText} - {CategoryText}";

    public string DecisionText
    {
        get => decisionText;
        private set => SetProperty(ref decisionText, value);
    }

    public void MarkConfirmed()
    {
        source = source.Confirm();
        DecisionText = "Confirmed and recorded";
    }

    public void MarkDelayed(DateOnly delayedUntil, DateDisplayFormat dateDisplayFormat)
    {
        source = source.DelayUntil(delayedUntil);
        DecisionText = $"Delayed until {DateDisplay.Format(delayedUntil, dateDisplayFormat)}";
    }

    public void MarkCancelled()
    {
        source = source.Cancel();
        DecisionText = "Skipped";
    }
}

public sealed class AccountSummaryViewModel
{
    public AccountSummaryViewModel(Account account)
    {
        Source = account;
    }

    public Account Source { get; }

    public string Name => Source.Name;

    public string TypeText => DisplayText.Format(Source.Type);

    public string BalanceText => $"{Source.Currency} {Source.CurrentBalance:N2}";

    public string IdentifierText
    {
        get
        {
            var accountNumber = string.IsNullOrWhiteSpace(Source.AccountNumber)
                ? null
                : $"Account {FormatAccountNumber(Source.AccountNumber)}";
            var card = string.IsNullOrWhiteSpace(Source.CardLastFourDigits)
                ? null
                : $"Card ending {Source.CardLastFourDigits}";

            return (accountNumber, card) switch
            {
                (not null, not null) => $"{accountNumber} - {card}",
                (not null, null) => accountNumber,
                (null, not null) => card,
                _ => "No identifier saved"
            };
        }
    }

    public string DashboardText => Source.IncludeInDashboardTotals
        ? "Included in dashboard total"
        : "Excluded from dashboard total";

    public string CreditText
    {
        get
        {
            if (Source.Type != AccountType.CreditCard)
            {
                return "Standard account";
            }

            var details = Source.CreditCardDetails;
            var debt = details?.CurrentDebt is null ? "debt not set" : $"{Source.Currency} {details.CurrentDebt:N2} debt";
            var payment = details?.PlannedPaymentAmount ?? details?.MinimumPayment;
            return payment is null ? debt : $"{debt}, {Source.Currency} {payment:N2} payment";
        }
    }

    private static string FormatAccountNumber(string value)
    {
        var normalized = string.Concat(value.Where(char.IsDigit));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return value.Trim();
        }

        return string.Join(
            ' ',
            Enumerable.Range(0, (normalized.Length + 3) / 4)
                .Select(index =>
                {
                    var start = index * 4;
                    var length = Math.Min(4, normalized.Length - start);
                    return normalized.Substring(start, length);
                }));
    }
}

public sealed class CategoryChoiceViewModel
{
    public static CategoryChoiceViewModel None { get; } = new(null, "None", null, IsNone: true, IsSuggestion: false);

    public static CategoryChoiceViewModel Suggestion(string name, TransactionType type)
    {
        return new CategoryChoiceViewModel(null, name, type, IsNone: false, IsSuggestion: true);
    }

    public CategoryChoiceViewModel(
        Guid? categoryId,
        string name,
        TransactionType? type,
        bool IsNone = false,
        bool IsSuggestion = false)
    {
        CategoryId = categoryId;
        Name = name;
        Type = type;
        this.IsNone = IsNone;
        this.IsSuggestion = IsSuggestion;
    }

    public Guid? CategoryId { get; }

    public string Name { get; }

    public TransactionType? Type { get; }

    public bool IsNone { get; }

    public bool IsSuggestion { get; }
}

public sealed class CategorySummaryViewModel
{
    public CategorySummaryViewModel(Category category)
    {
        Source = category;
    }

    public Category Source { get; }

    public string Name => Source.Name;

    public string TypeText => Source.Type is null ? "Income and expense" : DisplayText.Format(Source.Type);
}

internal sealed record DefaultCategoryDefinition(TransactionType Type, string Name);

public sealed class TransactionSummaryViewModel
{
    public TransactionSummaryViewModel(
        Transaction transaction,
        string accountName,
        string? destinationAccountName,
        string? destinationGoalName,
        string? categoryName,
        string currency,
        DateDisplayFormat dateDisplayFormat)
    {
        Source = transaction;
        AccountName = accountName;
        DestinationText = destinationAccountName is not null
            ? $"to {destinationAccountName}"
            : destinationGoalName is not null
                ? $"to goal {destinationGoalName}"
                : string.Empty;
        CategoryText = transaction.Type == TransactionType.Transfer
            ? string.IsNullOrWhiteSpace(DestinationText) ? "Transfer" : $"Transfer {DestinationText}"
            : categoryName ?? "None";
        AmountText = $"{currency} {transaction.Amount:N2}";
        DateText = DateDisplay.Format(transaction.Date, dateDisplayFormat);
        DisplayTitle = transaction.Type == TransactionType.Transfer
            ? $"{accountName} -> {destinationAccountName ?? destinationGoalName ?? "Unknown destination"}"
            : $"{accountName} - {CategoryText}";
    }

    public Transaction Source { get; }

    public string AccountName { get; }

    public string DestinationText { get; }

    public string CategoryText { get; }

    public string AmountText { get; }

    public string DateText { get; }

    public string DisplayTitle { get; }

    public string TypeText => DisplayText.Format(Source.Type);

    public string NotesText => string.IsNullOrWhiteSpace(Source.Notes) ? "No notes" : Source.Notes;
}

public sealed class ScheduledTransactionSummaryViewModel
{
    public ScheduledTransactionSummaryViewModel(
        ScheduledTransaction scheduledTransaction,
        string accountName,
        string? categoryName,
        string currency,
        DateDisplayFormat dateDisplayFormat)
    {
        Source = scheduledTransaction;
        AccountName = accountName;
        CategoryText = categoryName ?? "None";
        AmountText = $"{currency} {scheduledTransaction.Amount:N2}";
        NextText = DateDisplay.Format(scheduledTransaction.NextOccurrence, dateDisplayFormat);
    }

    public ScheduledTransaction Source { get; }

    public string AccountName { get; }

    public string CategoryText { get; }

    public string AmountText { get; }

    public string Name => Source.Name;

    public string TypeText => DisplayText.Format(Source.Type);

    public string NextText { get; }

    public string RecurrenceText => RecurrenceDisplay.Format(Source.RecurrenceRule);

    public string NotesText => string.IsNullOrWhiteSpace(Source.Notes) ? "No notes" : Source.Notes;
}

public sealed class SavingsGoalSummaryViewModel
{
    public SavingsGoalSummaryViewModel(
        SavingsGoal savingsGoal,
        string? accountName,
        string currency,
        DateDisplayFormat dateDisplayFormat)
    {
        Source = savingsGoal;
        AccountName = accountName ?? "No linked account";
        CurrentText = $"{currency} {savingsGoal.CurrentAmount:N2}";
        TargetText = $"{currency} {savingsGoal.TargetAmount:N2}";
        var progress = savingsGoal.TargetAmount <= 0m
            ? 0m
            : Math.Clamp(savingsGoal.CurrentAmount / savingsGoal.TargetAmount, 0m, 1m);
        ProgressText = $"{progress:P0}";
        TargetDateText = Source.TargetDate is null
            ? "No target date"
            : DateDisplay.Format(Source.TargetDate.Value, dateDisplayFormat);
    }

    public SavingsGoal Source { get; }

    public string AccountName { get; }

    public string CurrentText { get; }

    public string TargetText { get; }

    public string ProgressText { get; }

    public string Name => Source.Name;

    public string TargetDateText { get; }
}

public sealed class ForecastEventSummaryViewModel
{
    public ForecastEventSummaryViewModel(
        ForecastEvent forecastEvent,
        string currency,
        DateDisplayFormat dateDisplayFormat)
    {
        Source = forecastEvent;
        AmountText = $"{currency} {forecastEvent.SignedAmount:N2}";
        DateText = DateDisplay.Format(forecastEvent.Date, dateDisplayFormat);
    }

    public ForecastEvent Source { get; }

    public string DateText { get; }

    public string Name => Source.Name;

    public string AmountText { get; }

    public string KindText => DisplayText.Format(Source.Kind);
}

public sealed class UpcomingObligationSummaryViewModel
{
    public UpcomingObligationSummaryViewModel(
        UpcomingObligation obligation,
        string currency,
        DateDisplayFormat dateDisplayFormat)
    {
        Source = obligation;
        AmountText = $"{currency} {obligation.Amount:N2}";
        DateText = DateDisplay.Format(obligation.Date, dateDisplayFormat);
    }

    public UpcomingObligation Source { get; }

    public string DateText { get; }

    public string Name => Source.Name;

    public string AmountText { get; }

    public string KindText => DisplayText.Format(Source.Kind);
}

public sealed class StatementImportBatchSummaryViewModel
{
    public StatementImportBatchSummaryViewModel(
        StatementImportBatch batch,
        string accountName)
    {
        Source = batch;
        AccountName = accountName;
    }

    public StatementImportBatch Source { get; }

    public string AccountName { get; }

    public string DisplayTitle => $"{Source.SourceFileName} - {AccountName}";

    public string DetailText => $"{DisplayText.Format(Source.Status)} - {Source.RowCount} row(s) - {Source.ParserName}";
}

public sealed class StatementImportRowSummaryViewModel
{
    public StatementImportRowSummaryViewModel(
        StatementImportRow row,
        string? suggestedCategoryName,
        string? categoryName,
        string currency,
        DateDisplayFormat dateDisplayFormat,
        IEnumerable<CategoryChoiceViewModel> categoryChoices)
    {
        Source = row;
        SuggestedCategoryName = suggestedCategoryName ?? "None";
        CategoryName = categoryName ?? "None";
        AmountText = $"{currency} {row.Amount:N2}";
        DateText = DateDisplay.Format(row.Date, dateDisplayFormat);
        CategoryChoices = new ObservableCollection<CategoryChoiceViewModel>(categoryChoices);
        SelectedCategory = CreateSelectedCategory();
    }

    public StatementImportRow Source { get; }

    public string DateText { get; }

    public string Description => Source.Description;

    public string AmountText { get; }

    public string TypeText => DisplayText.Format(Source.Type);

    public string StatusText => DisplayText.Format(Source.Status);

    public string DuplicateText => Source.IsDuplicate ? "Possible duplicate" : "New";

    public ObservableCollection<CategoryChoiceViewModel> CategoryChoices { get; }

    public CategoryChoiceViewModel? SelectedCategory { get; set; }

    public bool IsPending => Source.Status == StatementImportRowStatus.Pending;

    public string SuggestedCategoryName { get; }

    public string CategoryName { get; }

    public string CategoryText => Source.CategoryId.HasValue
        ? $"Category: {CategoryName}"
        : Source.SuggestedCategoryId.HasValue
            ? $"Suggested: {SuggestedCategoryName}"
            : "No suggestion";

    private CategoryChoiceViewModel? CreateSelectedCategory()
    {
        if (Source.CategoryId is { } categoryId)
        {
            return CategoryChoices.FirstOrDefault(choice => choice.CategoryId == categoryId)
                ?? CategoryChoices.FirstOrDefault();
        }

        if (Source.SuggestedCategoryId is { } suggestedCategoryId)
        {
            return CategoryChoices.FirstOrDefault(choice => choice.CategoryId == suggestedCategoryId)
                ?? CategoryChoices.FirstOrDefault();
        }

        return CategoryChoices.FirstOrDefault();
    }
}

internal static class DisplayText
{
    public static string Format(object value)
    {
        return value.ToString() is { } text
            ? SplitPascalCase(text)
            : string.Empty;
    }

    private static string SplitPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var result = new List<char>(text.Length + 4) { text[0] };
        for (var index = 1; index < text.Length; index++)
        {
            var current = text[index];
            var previous = text[index - 1];
            if (char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous)))
            {
                result.Add(' ');
            }

            result.Add(current);
        }

        return new string(result.ToArray());
    }
}

internal static class RecurrenceDisplay
{
    public static string Format(RecurrenceRule recurrenceRule)
    {
        if (recurrenceRule.Interval <= 1)
        {
            return $"Every {GetPeriodName(recurrenceRule.Frequency, 1)}";
        }

        return $"Every {recurrenceRule.Interval} {GetPeriodName(recurrenceRule.Frequency, recurrenceRule.Interval)}";
    }

    public static string GetPeriodName(RecurrenceFrequency frequency, int interval)
    {
        var singular = frequency switch
        {
            RecurrenceFrequency.Daily => "day",
            RecurrenceFrequency.Weekly => "week",
            RecurrenceFrequency.Monthly => "month",
            RecurrenceFrequency.Yearly => "year",
            _ => DisplayText.Format(frequency).ToLowerInvariant()
        };

        if (interval <= 1)
        {
            return singular;
        }

        return $"{singular}s";
    }
}

internal static class DateDisplay
{
    public static string Format(DateOnly date, DateDisplayFormat format)
    {
        return date.ToString(GetPattern(format), CultureInfo.InvariantCulture);
    }

    public static string FormatShortWithoutYear(DateOnly date, DateDisplayFormat format)
    {
        return date.ToString(GetShortPatternWithoutYear(format), CultureInfo.InvariantCulture);
    }

    public static string GetPattern(DateDisplayFormat format)
    {
        return format switch
        {
            DateDisplayFormat.MonthDayYear => "MM/dd/yyyy",
            DateDisplayFormat.YearMonthDay => "yyyy-MM-dd",
            _ => "dd/MM/yyyy"
        };
    }

    private static string GetShortPatternWithoutYear(DateDisplayFormat format)
    {
        return format switch
        {
            DateDisplayFormat.MonthDayYear => "MM/dd",
            DateDisplayFormat.YearMonthDay => "MM-dd",
            _ => "dd/MM"
        };
    }
}
