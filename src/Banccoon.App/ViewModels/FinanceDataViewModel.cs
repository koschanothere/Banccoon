using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.Core.Abstractions;
using Banccoon.Core.CreditCards;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;

namespace Banccoon.App.ViewModels;

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
    private readonly IDateProvider dateProvider;
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
    private string forecastPeriodText = "30 days";
    private string newAccountName = string.Empty;
    private AccountType selectedAccountType = AccountType.DebitCard;
    private string newAccountBalanceText = "0";
    private string newAccountCurrency = "EUR";
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
    private Account? newTransactionAccount;
    private CategoryChoiceViewModel? selectedTransactionCategory;
    private string newTransactionAmountText = string.Empty;
    private string newTransactionNotes = string.Empty;
    private DateTime newTransactionDate;
    private TransactionType selectedTransactionType = TransactionType.Expense;
    private Account? newScheduledAccount;
    private CategoryChoiceViewModel? selectedScheduledCategory;
    private string newScheduledName = string.Empty;
    private string newScheduledAmountText = string.Empty;
    private DateTime newScheduledDate;
    private TransactionType selectedScheduledType = TransactionType.Expense;
    private RecurrenceFrequency selectedScheduledFrequency = RecurrenceFrequency.Monthly;
    private string newScheduledIntervalText = "1";
    private Account? newGoalAccount;
    private string newGoalName = string.Empty;
    private string newGoalTargetAmountText = string.Empty;
    private string newGoalCurrentAmountText = string.Empty;
    private DateTime newGoalTargetDate;
    private string newCategoryName = string.Empty;

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
        this.dateProvider = dateProvider;

        var today = dateProvider.Today.ToDateTime(TimeOnly.MinValue);
        newTransactionDate = today;
        newScheduledDate = today;
        newGoalTargetDate = today.AddMonths(6);

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AddAccountCommand = new AsyncRelayCommand(AddAccountAsync);
        SaveSelectedAccountCommand = new AsyncRelayCommand(SaveSelectedAccountAsync);
        DeleteAccountCommand = new AsyncRelayCommand<Account>(DeleteAccountAsync);
        AddTransactionCommand = new AsyncRelayCommand(AddTransactionAsync);
        DeleteTransactionCommand = new AsyncRelayCommand<Transaction>(DeleteTransactionAsync);
        AddCategoryCommand = new AsyncRelayCommand(AddCategoryAsync);
        DeleteCategoryCommand = new AsyncRelayCommand<Category>(DeleteCategoryAsync);
        AddScheduledTransactionCommand = new AsyncRelayCommand(AddScheduledTransactionAsync);
        DeleteScheduledTransactionCommand = new AsyncRelayCommand<ScheduledTransaction>(DeleteScheduledTransactionAsync);
        AddSavingsGoalCommand = new AsyncRelayCommand(AddSavingsGoalAsync);
        DeleteSavingsGoalCommand = new AsyncRelayCommand<SavingsGoal>(DeleteSavingsGoalAsync);
        SavePreferencesCommand = new AsyncRelayCommand(SavePreferencesAsync);
    }

    public ObservableCollection<Account> Accounts { get; } = new();

    public ObservableCollection<AccountSummaryViewModel> AccountSummaries { get; } = new();

    public ObservableCollection<Category> Categories { get; } = new();

    public ObservableCollection<CategoryChoiceViewModel> CategoryChoices { get; } = new();

    public ObservableCollection<CategorySummaryViewModel> CategorySummaries { get; } = new();

    public ObservableCollection<Transaction> Transactions { get; } = new();

    public ObservableCollection<TransactionSummaryViewModel> TransactionSummaries { get; } = new();

    public ObservableCollection<ScheduledTransaction> ScheduledTransactions { get; } = new();

    public ObservableCollection<ScheduledTransactionSummaryViewModel> ScheduledTransactionSummaries { get; } = new();

    public ObservableCollection<SavingsGoal> SavingsGoals { get; } = new();

    public ObservableCollection<SavingsGoalSummaryViewModel> SavingsGoalSummaries { get; } = new();

    public ObservableCollection<ForecastEventSummaryViewModel> ForecastEvents { get; } = new();

    public ObservableCollection<UpcomingObligationSummaryViewModel> UpcomingObligations { get; } = new();

    public IReadOnlyList<AccountType> AccountTypes { get; } = Enum.GetValues<AccountType>();

    public IReadOnlyList<TransactionType> TransactionTypes { get; } = Enum.GetValues<TransactionType>();

    public IReadOnlyList<RecurrenceFrequency> RecurrenceFrequencies { get; } = Enum.GetValues<RecurrenceFrequency>();

    public IReadOnlyList<ForecastPeriod> ForecastPeriods { get; } = Enum.GetValues<ForecastPeriod>();

    public IReadOnlyList<ReminderFrequency> ReminderFrequencies { get; } = Enum.GetValues<ReminderFrequency>();

    public IReadOnlyList<DateDisplayFormat> DateDisplayFormats { get; } = Enum.GetValues<DateDisplayFormat>();

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

    public ICommand DeleteTransactionCommand { get; }

    public ICommand AddCategoryCommand { get; }

    public ICommand DeleteCategoryCommand { get; }

    public ICommand AddScheduledTransactionCommand { get; }

    public ICommand DeleteScheduledTransactionCommand { get; }

    public ICommand AddSavingsGoalCommand { get; }

    public ICommand DeleteSavingsGoalCommand { get; }

    public ICommand SavePreferencesCommand { get; }

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
            }
        }
    }

    public bool IsNewAccountCreditCard => SelectedAccountType == AccountType.CreditCard;

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
                RecalculateSelectedAccountPayoff();
            }
        }
    }

    public bool IsSelectedAccountCreditCard => SelectedAccount?.Type == AccountType.CreditCard;

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

    public CategoryChoiceViewModel? SelectedTransactionCategory
    {
        get => selectedTransactionCategory;
        set => SetProperty(ref selectedTransactionCategory, value);
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
        set => SetProperty(ref selectedTransactionType, value);
    }

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
        set => SetProperty(ref selectedScheduledType, value);
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

    public string NewCategoryName
    {
        get => newCategoryName;
        set => SetProperty(ref newCategoryName, value);
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

        var creditCardDetails = CreateCreditCardDetailsForNewAccount();
        var account = new Account(
            Guid.NewGuid(),
            NewAccountName.Trim(),
            SelectedAccountType,
            balance,
            NormalizeCurrency(NewAccountCurrency),
            DateTimeOffset.UtcNow,
            IsArchived: false,
            creditCardDetails);

        await accountRepository.SaveAsync(account);
        NewAccountName = string.Empty;
        NewAccountBalanceText = "0";
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

        if (!TryReadDecimal(SelectedAccountBalanceText, out var balance))
        {
            SetStatus("Updated balance must be a number.");
            return;
        }

        var updatedAccount = SelectedAccount with
        {
            CurrentBalance = balance,
            CreditCardDetails = CreateCreditCardDetailsForSelectedAccount()
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

        var category = new Category(Guid.NewGuid(), NewCategoryName.Trim());
        await categoryRepository.SaveAsync(category);
        NewCategoryName = string.Empty;
        await RefreshAfterMutationAsync("Category saved.");
    }

    private async Task DeleteCategoryAsync(Category category)
    {
        await categoryRepository.DeleteAsync(category.Id);
        await RefreshAfterMutationAsync("Category deleted.");
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

        var transaction = new Transaction(
            Guid.NewGuid(),
            DateOnly.FromDateTime(NewTransactionDate),
            amount,
            NewTransactionAccount.Id,
            SelectedTransactionCategory?.CategoryId,
            string.IsNullOrWhiteSpace(NewTransactionNotes) ? null : NewTransactionNotes.Trim(),
            SelectedTransactionType);

        var updatedAccount = transactionBalanceService.Apply(NewTransactionAccount, transaction);
        await accountRepository.SaveAsync(updatedAccount);
        await transactionRepository.SaveAsync(transaction);
        NewTransactionAmountText = string.Empty;
        NewTransactionNotes = string.Empty;
        await RefreshAfterMutationAsync("Transaction saved.");
    }

    private async Task DeleteTransactionAsync(Transaction transaction)
    {
        var account = await accountRepository.GetByIdAsync(transaction.AccountId);
        if (account is not null)
        {
            await accountRepository.SaveAsync(transactionBalanceService.Reverse(account, transaction));
        }

        await transactionRepository.DeleteAsync(transaction.Id);
        await RefreshAfterMutationAsync("Transaction deleted.");
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
        var scheduledTransaction = new ScheduledTransaction(
            Guid.NewGuid(),
            NewScheduledName.Trim(),
            amount,
            NewScheduledAccount.Id,
            SelectedScheduledCategory?.CategoryId,
            SelectedScheduledType,
            recurrenceRule,
            startDate,
            Active: true);

        await scheduledTransactionRepository.SaveAsync(scheduledTransaction);
        NewScheduledName = string.Empty;
        NewScheduledAmountText = string.Empty;
        await RefreshAfterMutationAsync("Scheduled item saved.");
    }

    private async Task DeleteScheduledTransactionAsync(ScheduledTransaction scheduledTransaction)
    {
        await scheduledTransactionRepository.DeleteAsync(scheduledTransaction.Id);
        await RefreshAfterMutationAsync("Scheduled item deleted.");
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
        if (SelectedAccount?.Type != AccountType.CreditCard)
        {
            return SelectedAccount?.CreditCardDetails;
        }

        return new CreditCardDetails(
            ReadOptionalDecimal(SelectedAccountDebtText),
            SelectedAccount.CreditCardDetails?.StatementDayOfMonth,
            ReadOptionalPaymentDueDay(SelectedAccountPaymentDueDayText),
            ReadOptionalDecimal(SelectedAccountMinimumPaymentText),
            ReadOptionalDecimal(SelectedAccountPlannedPaymentText));
    }

    private void LoadSelectedAccountEditor(Account? account)
    {
        if (account is null)
        {
            SelectedAccountBalanceText = string.Empty;
            SelectedAccountDebtText = string.Empty;
            SelectedAccountMinimumPaymentText = string.Empty;
            SelectedAccountPlannedPaymentText = string.Empty;
            SelectedAccountPaymentDueDayText = string.Empty;
            SelectedAccountPayoffPaymentText = string.Empty;
            return;
        }

        SelectedAccountBalanceText = ToInputText(account.CurrentBalance);
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
        NewScheduledAccount = FindFreshAccount(NewScheduledAccount) ?? Accounts.FirstOrDefault();
        NewGoalAccount = FindFreshAccount(NewGoalAccount)
            ?? Accounts.FirstOrDefault(account => account.Type == AccountType.Savings)
            ?? Accounts.FirstOrDefault();
        SelectedTransactionCategory = FindFreshCategoryChoice(SelectedTransactionCategory) ?? CategoryChoices.FirstOrDefault();
        SelectedScheduledCategory = FindFreshCategoryChoice(SelectedScheduledCategory) ?? CategoryChoices.FirstOrDefault();
    }

    private Account? FindFreshAccount(Account? account)
    {
        return account is null
            ? null
            : Accounts.FirstOrDefault(candidate => candidate.Id == account.Id);
    }

    private CategoryChoiceViewModel? FindFreshCategoryChoice(CategoryChoiceViewModel? categoryChoice)
    {
        return categoryChoice is null
            ? null
            : CategoryChoices.FirstOrDefault(candidate => candidate.CategoryId == categoryChoice.CategoryId);
    }

    private void UpdateCategoryChoices()
    {
        var choices = new[]
            {
                CategoryChoiceViewModel.None
            }
            .Concat(Categories.Select(category => new CategoryChoiceViewModel(category.Id, category.Name)));

        Replace(CategoryChoices, choices);
    }

    private void UpdateSummaries()
    {
        Replace(AccountSummaries, Accounts.Select(account => new AccountSummaryViewModel(account)));
        Replace(CategorySummaries, Categories.Select(category => new CategorySummaryViewModel(category)));
        Replace(TransactionSummaries, Transactions.Select(transaction => new TransactionSummaryViewModel(
            transaction,
            Accounts.FirstOrDefault(account => account.Id == transaction.AccountId)?.Name ?? "Unknown account",
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
        if (Accounts.Count == 0)
        {
            CurrentBalanceText = FormatMoney(0m, DefaultCurrency);
            AvailableToSpendText = FormatMoney(0m, DefaultCurrency);
            LowestForecastText = FormatMoney(0m, DefaultCurrency);
            UpcomingObligationsText = FormatMoney(0m, DefaultCurrency);
            ForecastPeriodText = GetForecastPeriodLabel(SelectedForecastPeriod);
            ForecastEvents.Clear();
            UpcomingObligations.Clear();
            OnPropertyChanged(nameof(HasForecastEvents));
            OnPropertyChanged(nameof(HasUpcomingObligations));
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
        ForecastPeriodText = GetForecastPeriodLabel(SelectedForecastPeriod);

        Replace(ForecastEvents, forecast.Events.Select(forecastEvent => new ForecastEventSummaryViewModel(
            forecastEvent,
            DefaultCurrency,
            SelectedDateDisplayFormat)));
        Replace(UpcomingObligations, forecast.UpcomingObligations.Select(obligation => new UpcomingObligationSummaryViewModel(
            obligation,
            DefaultCurrency,
            SelectedDateDisplayFormat)));
        OnPropertyChanged(nameof(HasForecastEvents));
        OnPropertyChanged(nameof(HasUpcomingObligations));
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
}

public sealed class CategoryChoiceViewModel
{
    public static CategoryChoiceViewModel None { get; } = new(null, "None");

    public CategoryChoiceViewModel(Guid? categoryId, string name)
    {
        CategoryId = categoryId;
        Name = name;
    }

    public Guid? CategoryId { get; }

    public string Name { get; }
}

public sealed class CategorySummaryViewModel
{
    public CategorySummaryViewModel(Category category)
    {
        Source = category;
    }

    public Category Source { get; }

    public string Name => Source.Name;
}

public sealed class TransactionSummaryViewModel
{
    public TransactionSummaryViewModel(
        Transaction transaction,
        string accountName,
        string? categoryName,
        string currency,
        DateDisplayFormat dateDisplayFormat)
    {
        Source = transaction;
        AccountName = accountName;
        CategoryText = categoryName ?? "None";
        AmountText = $"{currency} {transaction.Amount:N2}";
        DateText = DateDisplay.Format(transaction.Date, dateDisplayFormat);
    }

    public Transaction Source { get; }

    public string AccountName { get; }

    public string CategoryText { get; }

    public string AmountText { get; }

    public string DateText { get; }

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

    public static string GetPattern(DateDisplayFormat format)
    {
        return format switch
        {
            DateDisplayFormat.MonthDayYear => "MM/dd/yyyy",
            DateDisplayFormat.YearMonthDay => "yyyy-MM-dd",
            _ => "dd/MM/yyyy"
        };
    }
}
