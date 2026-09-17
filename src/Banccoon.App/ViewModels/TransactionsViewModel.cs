using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Categories;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;

namespace Banccoon.App.ViewModels;

public sealed class TransactionsViewModel : ViewModelBase
{
    private static readonly Guid AllOptionId = Guid.Empty;

    private readonly IAccountRepository accountRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly ISettingsRepository settingsRepository;
    private readonly ITransactionBalanceHistoryService transactionBalanceHistoryService;

    private IReadOnlyList<Account> accounts = [];
    private IReadOnlyList<Category> categories = [];
    private IReadOnlyList<Transaction> allTransactions = [];
    private string currency = "EUR";

    private bool isLoading;
    private bool filterOpen;
    private bool selectMode;
    private bool addMenuOpen;
    private NamedOptionViewModel? accountFilter;
    private NamedOptionViewModel? categoryFilter;
    private NamedOptionViewModel? bulkCategory;
    private string selectionSummaryText = string.Empty;

    public TransactionsViewModel(
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
        ITransactionBalanceHistoryService transactionBalanceHistoryService,
        ICategoryManagementService categoryManagementService)
    {
        this.accountRepository = accountRepository;
        this.categoryRepository = categoryRepository;
        this.transactionRepository = transactionRepository;
        this.settingsRepository = settingsRepository;
        this.transactionBalanceHistoryService = transactionBalanceHistoryService;

        AddForm = new NewTransactionFormViewModel(
            accountRepository,
            categoryRepository,
            transactionRepository,
            transactionApplicationService,
            InitializeAsync);
        ResolveUpcoming = new ResolveUpcomingListViewModel(
            dateProvider,
            accountRepository,
            transactionRepository,
            scheduledTransactionRepository,
            scheduledOccurrenceOverrideRepository,
            scheduledTransactionProjectionService,
            scheduledOccurrenceResolutionService,
            transactionApplicationService,
            InitializeAsync);
        CategoryManagement = new CategoryManagementViewModel(
            categoryRepository,
            categoryManagementService,
            InitializeAsync);

        Rows = [];
        AccountOptions = [];
        CategoryOptions = [];
        BulkCategoryOptions = [];

        ToggleFilterCommand = new RelayCommand(() => FilterOpen = !FilterOpen);
        ToggleSelectModeCommand = new RelayCommand(ToggleSelectMode);
        ToggleAddMenuCommand = new RelayCommand(() => AddMenuOpen = !AddMenuOpen);
        OpenExpenseFormCommand = new RelayCommand(() => OpenAddForm(TransactionType.Expense));
        OpenIncomeFormCommand = new RelayCommand(() => OpenAddForm(TransactionType.Income));
        OpenTransferFormCommand = new RelayCommand(() => OpenAddForm(TransactionType.Transfer));
        DeleteSelectedCommand = new RelayCommand(() => _ = DeleteSelectedAsync());
        AssignCategoryToSelectedCommand = new RelayCommand(() => _ = AssignCategoryToSelectedAsync());
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public bool FilterOpen
    {
        get => filterOpen;
        private set => SetProperty(ref filterOpen, value);
    }

    public bool SelectMode
    {
        get => selectMode;
        private set => SetProperty(ref selectMode, value);
    }

    public bool AddMenuOpen
    {
        get => addMenuOpen;
        private set => SetProperty(ref addMenuOpen, value);
    }

    public NamedOptionViewModel? AccountFilter
    {
        get => accountFilter;
        set
        {
            if (SetProperty(ref accountFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public NamedOptionViewModel? CategoryFilter
    {
        get => categoryFilter;
        set
        {
            if (SetProperty(ref categoryFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectionSummaryText
    {
        get => selectionSummaryText;
        private set => SetProperty(ref selectionSummaryText, value);
    }

    public NamedOptionViewModel? BulkCategory
    {
        get => bulkCategory;
        set => SetProperty(ref bulkCategory, value);
    }

    public NewTransactionFormViewModel AddForm { get; }

    public ResolveUpcomingListViewModel ResolveUpcoming { get; }

    public CategoryManagementViewModel CategoryManagement { get; }

    public ObservableCollection<TransactionRowViewModel> Rows { get; }

    public ObservableCollection<NamedOptionViewModel> AccountOptions { get; }

    public ObservableCollection<NamedOptionViewModel> CategoryOptions { get; }

    public ObservableCollection<NamedOptionViewModel> BulkCategoryOptions { get; }

    public ICommand ToggleFilterCommand { get; }

    public ICommand ToggleSelectModeCommand { get; }

    public ICommand ToggleAddMenuCommand { get; }

    public ICommand OpenExpenseFormCommand { get; }

    public ICommand OpenIncomeFormCommand { get; }

    public ICommand OpenTransferFormCommand { get; }

    public ICommand DeleteSelectedCommand { get; }

    public ICommand AssignCategoryToSelectedCommand { get; }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var settings = await settingsRepository.GetAsync();
            currency = settings.DefaultCurrency;

            accounts = await accountRepository.GetAllAsync();
            categories = await categoryRepository.GetAllAsync();
            allTransactions = await transactionRepository.GetAllAsync();

            RebuildOptionLists();
            ApplyFilters();
            await ResolveUpcoming.RefreshAsync(currency);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RebuildOptionLists()
    {
        AccountOptions.Clear();
        AccountOptions.Add(new NamedOptionViewModel(AllOptionId, "All accounts"));
        foreach (var account in accounts.Where(account => !account.IsArchived).OrderBy(account => account.Name))
        {
            AccountOptions.Add(new NamedOptionViewModel(account.Id, account.Name));
        }

        CategoryOptions.Clear();
        CategoryOptions.Add(new NamedOptionViewModel(AllOptionId, "All categories"));
        BulkCategoryOptions.Clear();
        foreach (var category in categories.OrderBy(category => category.Name))
        {
            CategoryOptions.Add(new NamedOptionViewModel(category.Id, category.Name));
            BulkCategoryOptions.Add(new NamedOptionViewModel(category.Id, category.Name));
        }

        BulkCategory = BulkCategoryOptions.FirstOrDefault();
    }

    private void ApplyFilters()
    {
        var accountsById = accounts.ToDictionary(account => account.Id);
        var balancesByAccount = accounts.ToDictionary(
            account => account.Id,
            account => transactionBalanceHistoryService.GetBalancesAfterEachTransaction(
                account,
                allTransactions
                    .Where(transaction => transaction.AccountId == account.Id || transaction.DestinationAccountId == account.Id)
                    .ToList()));

        var filtered = allTransactions.AsEnumerable();
        if (AccountFilter is not null && AccountFilter.Id != AllOptionId)
        {
            filtered = filtered.Where(transaction =>
                transaction.AccountId == AccountFilter.Id || transaction.DestinationAccountId == AccountFilter.Id);
        }

        if (CategoryFilter is not null && CategoryFilter.Id != AllOptionId)
        {
            filtered = filtered.Where(transaction => transaction.CategoryId == CategoryFilter.Id);
        }

        Rows.Clear();
        foreach (var transaction in filtered.OrderByDescending(transaction => transaction.Date))
        {
            var descriptionText = GetCategoryOrDestinationText(transaction, accountsById);
            var balanceAfter = balancesByAccount.TryGetValue(transaction.AccountId, out var balances) && balances.TryGetValue(transaction.Id, out var balance)
                ? balance
                : accountsById.TryGetValue(transaction.AccountId, out var account) ? account.CurrentBalance : 0m;

            var row = new TransactionRowViewModel(transaction, descriptionText, balanceAfter, currency)
            {
                IsSelectModeActive = SelectMode
            };
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TransactionRowViewModel.IsSelected))
                {
                    UpdateSelectionSummary();
                }
            };
            Rows.Add(row);
        }

        UpdateSelectionSummary();
    }

    private string GetCategoryOrDestinationText(Transaction transaction, IReadOnlyDictionary<Guid, Account> accountsById)
    {
        if (transaction.Type == TransactionType.Transfer && transaction.DestinationAccountId is { } destinationId)
        {
            return accountsById.TryGetValue(destinationId, out var destination)
                ? $"Transfer to {destination.Name}"
                : "Transfer";
        }

        if (transaction.CategoryId is { } categoryId)
        {
            var category = categories.FirstOrDefault(category => category.Id == categoryId);
            if (category is not null)
            {
                return category.Name;
            }
        }

        return "Other";
    }

    private void ToggleSelectMode()
    {
        SelectMode = !SelectMode;
        foreach (var row in Rows)
        {
            row.IsSelectModeActive = SelectMode;
            if (!SelectMode)
            {
                row.IsSelected = false;
            }
        }

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var count = Rows.Count(row => row.IsSelected);
        SelectionSummaryText = count == 1 ? "1 selected" : $"{count} selected";
    }

    private void OpenAddForm(TransactionType type)
    {
        AddMenuOpen = false;
        _ = AddForm.OpenAsync(type);
    }

    private async Task DeleteSelectedAsync()
    {
        var selectedIds = Rows.Where(row => row.IsSelected).Select(row => row.Id).ToList();
        foreach (var id in selectedIds)
        {
            await transactionRepository.DeleteAsync(id);
        }

        SelectMode = false;
        await InitializeAsync();
    }

    private async Task AssignCategoryToSelectedAsync()
    {
        if (BulkCategory is null)
        {
            return;
        }

        var selectedIds = Rows.Where(row => row.IsSelected).Select(row => row.Id).ToHashSet();
        foreach (var transaction in allTransactions.Where(transaction => selectedIds.Contains(transaction.Id)))
        {
            await transactionRepository.SaveAsync(transaction with { CategoryId = BulkCategory.Id });
        }

        SelectMode = false;
        await InitializeAsync();
    }
}
