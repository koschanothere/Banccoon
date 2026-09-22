using System.Collections.ObjectModel;
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

public sealed class TransactionsViewModel : ViewModelBase
{
    private static readonly Guid AllOptionId = Guid.Empty;
    private const int PageSize = 20;

    private readonly IAccountRepository accountRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly ISettingsRepository settingsRepository;
    private readonly ITransactionBalanceHistoryService transactionBalanceHistoryService;

    private IReadOnlyList<Account> accounts = [];
    private IReadOnlyList<Category> categories = [];
    private IReadOnlyList<Transaction> allTransactions = [];
    private IReadOnlyList<Transaction> filteredTransactions = [];
    private IReadOnlyDictionary<Guid, Account> accountsById = new Dictionary<Guid, Account>();
    private IReadOnlyDictionary<Guid, Category> categoriesById = new Dictionary<Guid, Category>();
    private IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, decimal>> balancesByAccount =
        new Dictionary<Guid, IReadOnlyDictionary<Guid, decimal>>();
    private int visibleCount = PageSize;
    private string currency = "EUR";
    private Guid? pendingCategoryFilterId;
    private Guid? pendingAccountFilterId;

    // Clearing/repopulating AccountOptions or CategoryOptions makes their bound Pickers reset
    // their own SelectedItem (the native control's reaction to its ItemsSource changing), which
    // round-trips back through the two-way binding into AccountFilter/CategoryFilter's setters
    // below - reentrantly, from inside RebuildOptionLists, before it's restored the "real"
    // selection. That reentrant ApplyFilters() call raced against an in-flight CollectionView
    // scroll/arrange pass and threw a native "modification in progress" COMException (caught via
    // diagnostics.log, not reported by a user). This flag makes RebuildOptionLists the only thing
    // that runs while it's active - it always calls ApplyFilters() itself once done anyway.
    private bool isRebuildingOptions;

    // RebuildVisibleRows()'s Rows.Clear()+Add() loop can, while still mid-loop, synchronously
    // trigger the CollectionView's own RemainingItemsThresholdReached (crossing the "5 from the
    // end" threshold as rows are added back in) - which calls LoadMore(), which calls Rows.Add()
    // again while the native control is still dispatching the CollectionChanged event from the
    // rebuild's own Add() call. That reentrant Add() threw a real, user-hit
    // "InvalidOperationException: Cannot change ObservableCollection during a CollectionChanged
    // event" crash (caught via diagnostics.log). This flag makes a rebuild-in-progress the only
    // thing allowed to touch Rows; a reentrant LoadMore() just no-ops, since RebuildVisibleRows
    // already loads up to the current visibleCount itself.
    private bool isUpdatingRows;

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
        IRecurrenceDescriptionService recurrenceDescriptionService,
        IRecurrenceSyntaxService recurrenceSyntaxService,
        IRecurrenceValidationService recurrenceValidationService,
        IExpectedTransactionMatcher expectedTransactionMatcher)
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
            settingsRepository,
            () => InitializeAsync());
        ScheduleForm = new ScheduleFormViewModel(
            dateProvider,
            accountRepository,
            categoryRepository,
            scheduledTransactionRepository,
            recurrenceDescriptionService,
            recurrenceSyntaxService,
            recurrenceValidationService,
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
            recurrenceDescriptionService,
            expectedTransactionMatcher,
            InitializeAsync,
            onEditRequested: schedule => OpenScheduleFormForEditAsync(schedule));

        Rows = [];
        AccountOptions = [];
        CategoryOptions = [];
        BulkCategoryOptions = [];

        ToggleFilterCommand = new RelayCommand(ToggleFilterPanel);
        ToggleSelectModeCommand = new RelayCommand(ToggleSelectMode);
        ToggleAddMenuCommand = new RelayCommand(ToggleAddMenuPanel);
        OpenScheduleFormCommand = new RelayCommand(() => _ = OpenScheduleFormAsync());
        OpenExpenseFormCommand = new RelayCommand(() => OpenAddForm(TransactionType.Expense));
        OpenIncomeFormCommand = new RelayCommand(() => OpenAddForm(TransactionType.Income));
        OpenTransferFormCommand = new RelayCommand(() => OpenAddForm(TransactionType.Transfer));
        DeleteSelectedCommand = new RelayCommand(() => _ = DeleteSelectedAsync());
        AssignCategoryToSelectedCommand = new RelayCommand(() => _ = AssignCategoryToSelectedAsync());
        LoadMoreCommand = new RelayCommand(LoadMore);
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
            if (SetProperty(ref accountFilter, value) && !isRebuildingOptions)
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
            if (SetProperty(ref categoryFilter, value) && !isRebuildingOptions)
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

    public bool HasMoreRows => visibleCount < filteredTransactions.Count;

    public string RowCountText => filteredTransactions.Count == 0
        ? string.Empty
        : string.Format(Translator.Get("Transactions_RowCountFormat"), Rows.Count, filteredTransactions.Count);

    public NewTransactionFormViewModel AddForm { get; }

    public ResolveUpcomingListViewModel ResolveUpcoming { get; }

    public ScheduleFormViewModel ScheduleForm { get; }

    public ObservableCollection<TransactionRowViewModel> Rows { get; }

    public ObservableCollection<NamedOptionViewModel> AccountOptions { get; }

    public ObservableCollection<NamedOptionViewModel> CategoryOptions { get; }

    public ObservableCollection<NamedOptionViewModel> BulkCategoryOptions { get; }

    public ICommand ToggleFilterCommand { get; }

    public ICommand ToggleSelectModeCommand { get; }

    public ICommand ToggleAddMenuCommand { get; }

    public ICommand OpenScheduleFormCommand { get; }

    public ICommand OpenExpenseFormCommand { get; }

    public ICommand OpenIncomeFormCommand { get; }

    public ICommand OpenTransferFormCommand { get; }

    public ICommand DeleteSelectedCommand { get; }

    public ICommand AssignCategoryToSelectedCommand { get; }

    public ICommand LoadMoreCommand { get; }

    // Set when navigated here from the Dashboard's Analytics "drill down into this category"
    // action (see TransactionsPage.ApplyQueryAttributes) - applied once CategoryOptions is
    // rebuilt below, since the filter has to match an option already in that list by Id.
    public void SetPendingCategoryFilter(Guid categoryId)
    {
        pendingCategoryFilterId = categoryId;
    }

    // Set when navigated here from an account's detail card on Accounts ("View transactions").
    // Same timing rationale as SetPendingCategoryFilter above.
    public void SetPendingAccountFilter(Guid accountId)
    {
        pendingAccountFilterId = accountId;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var settings = await settingsRepository.GetAsync();
            currency = settings.DefaultCurrency;
            PrivacyMode.IsEnabled = settings.PrivacyModeEnabled;

            accounts = await accountRepository.GetAllAsync();
            categories = await categoryRepository.GetAllAsync();
            allTransactions = await transactionRepository.GetAllAsync();
            RebuildLookups();

            // Both mutate collections bound to live UI (Pickers/CollectionView) - must run on the
            // UI thread, which the awaits above may have hopped off of (see ViewModelBase.RunOnMainThreadAsync).
            await RunOnMainThreadAsync(() =>
            {
                RebuildOptionLists();
                ApplyPendingCategoryFilter();
                ApplyPendingAccountFilter();
                ApplyFilters();
            });
            await ResolveUpcoming.RefreshAsync(currency, settings.ResolveUpcomingNearTermDays);
        }
        finally
        {
            // Touches UI-bound state after an await that may have resumed off the UI thread (see
            // ViewModelBase.RunOnMainThreadAsync).
            await RunOnMainThreadAsync(() => IsLoading = false);
        }
    }

    private void RebuildOptionLists()
    {
        isRebuildingOptions = true;
        try
        {
            var previousAccountFilterId = AccountFilter?.Id;
            var previousCategoryFilterId = CategoryFilter?.Id;
            var previousBulkCategoryId = BulkCategory?.Id;

            AccountOptions.Clear();
            AccountOptions.Add(new NamedOptionViewModel(AllOptionId, Translator.Get("Transactions_AllAccountsOption")));
            foreach (var account in accounts.Where(account => !account.IsArchived).OrderBy(account => account.Name))
            {
                AccountOptions.Add(new NamedOptionViewModel(account.Id, account.Name));
            }

            CategoryOptions.Clear();
            CategoryOptions.Add(new NamedOptionViewModel(AllOptionId, Translator.Get("Transactions_AllCategoriesOption")));
            BulkCategoryOptions.Clear();
            foreach (var category in categories.OrderBy(category => category.Name))
            {
                CategoryOptions.Add(new NamedOptionViewModel(category.Id, category.Name));
                BulkCategoryOptions.Add(new NamedOptionViewModel(category.Id, category.Name));
            }

            // Re-resolve filter/bulk selections against the freshly rebuilt option lists by Id,
            // since RebuildOptionLists runs on every InitializeAsync (including every OnAppearing) -
            // without this, the Picker's SelectedItem no longer matches any object in the new
            // ItemsSource by reference, so it silently resets and the filter appears to "stop working".
            accountFilter = previousAccountFilterId is { } accountId
                ? AccountOptions.FirstOrDefault(option => option.Id == accountId)
                : AccountOptions.FirstOrDefault();
            OnPropertyChanged(nameof(AccountFilter));

            categoryFilter = previousCategoryFilterId is { } categoryId
                ? CategoryOptions.FirstOrDefault(option => option.Id == categoryId)
                : CategoryOptions.FirstOrDefault();
            OnPropertyChanged(nameof(CategoryFilter));

            bulkCategory = previousBulkCategoryId is { } bulkCategoryId
                ? BulkCategoryOptions.FirstOrDefault(option => option.Id == bulkCategoryId)
                : BulkCategoryOptions.FirstOrDefault();
            OnPropertyChanged(nameof(BulkCategory));
        }
        finally
        {
            isRebuildingOptions = false;
        }
    }

    private void ApplyPendingCategoryFilter()
    {
        if (pendingCategoryFilterId is not { } categoryId)
        {
            return;
        }

        var match = CategoryOptions.FirstOrDefault(option => option.Id == categoryId);
        if (match is not null)
        {
            CategoryFilter = match;
            FilterOpen = true;
        }

        pendingCategoryFilterId = null;
    }

    private void ApplyPendingAccountFilter()
    {
        if (pendingAccountFilterId is not { } accountId)
        {
            return;
        }

        var match = AccountOptions.FirstOrDefault(option => option.Id == accountId);
        if (match is not null)
        {
            AccountFilter = match;
            FilterOpen = true;
        }

        pendingAccountFilterId = null;
    }

    // Only depends on accounts/categories/allTransactions, which are only re-fetched from
    // InitializeAsync - not on AccountFilter/CategoryFilter, so this must not run from
    // ApplyFilters(). It used to, which meant every filter-picker change (and the pending-filter
    // re-application below) recomputed a full running-balance history for every account from
    // scratch - wasted work that grows with the account's transaction count and made the filter
    // panel feel sluggish on accounts with a lot of history.
    private void RebuildLookups()
    {
        accountsById = accounts.ToDictionary(account => account.Id);
        categoriesById = categories.ToDictionary(category => category.Id);
        balancesByAccount = accounts.ToDictionary(
            account => account.Id,
            account => transactionBalanceHistoryService.GetBalancesAfterEachTransaction(
                account,
                allTransactions
                    .Where(transaction => transaction.AccountId == account.Id || transaction.DestinationAccountId == account.Id)
                    .ToList()));
    }

    private void ApplyFilters()
    {
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

        filteredTransactions = filtered.OrderByDescending(transaction => transaction.Date).ToList();
        // Every filter change (account/category picker) starts back at page one - a "load more"
        // scroll position from the previous filter wouldn't mean anything against a new result set.
        visibleCount = PageSize;
        RebuildVisibleRows();
    }

    private void LoadMore()
    {
        // Appends only the newly-revealed rows rather than clearing and rebuilding the whole list
        // (see RebuildVisibleRows) - this runs from CollectionView's own
        // RemainingItemsThresholdReached, i.e. while it's actively mid-scroll, and a full Clear()
        // there previously raced the native control's own in-flight layout pass and threw a
        // "collection modification already in progress" COMException (caught via diagnostics.log).
        // Appending is a much smaller, additive change the control handles safely during a scroll.
        if (isUpdatingRows)
        {
            return;
        }

        isUpdatingRows = true;
        try
        {
            var previousVisibleCount = visibleCount;
            visibleCount += PageSize;

            foreach (var transaction in filteredTransactions.Skip(previousVisibleCount).Take(visibleCount - previousVisibleCount))
            {
                Rows.Add(BuildRow(transaction));
            }

            OnPropertyChanged(nameof(HasMoreRows));
            OnPropertyChanged(nameof(RowCountText));
        }
        finally
        {
            isUpdatingRows = false;
        }
    }

    private void RebuildVisibleRows()
    {
        isUpdatingRows = true;
        try
        {
            Rows.Clear();
            foreach (var transaction in filteredTransactions.Take(visibleCount))
            {
                Rows.Add(BuildRow(transaction));
            }

            OnPropertyChanged(nameof(HasMoreRows));
            OnPropertyChanged(nameof(RowCountText));
            UpdateSelectionSummary();
        }
        finally
        {
            isUpdatingRows = false;
        }
    }

    private TransactionRowViewModel BuildRow(Transaction transaction)
    {
        var descriptionText = GetCategoryOrDestinationText(transaction, accountsById);
        var balanceAfter = balancesByAccount.TryGetValue(transaction.AccountId, out var balances) && balances.TryGetValue(transaction.Id, out var balance)
            ? balance
            : accountsById.TryGetValue(transaction.AccountId, out var account) ? account.CurrentBalance : 0m;

        var category = transaction.CategoryId is { } categoryId && categoriesById.TryGetValue(categoryId, out var foundCategory)
            ? foundCategory
            : null;
        var row = new TransactionRowViewModel(transaction, descriptionText, balanceAfter, currency, category)
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
        return row;
    }

    private string GetCategoryOrDestinationText(Transaction transaction, IReadOnlyDictionary<Guid, Account> accountsById)
    {
        if (transaction.Type == TransactionType.Transfer && transaction.DestinationAccountId is { } destinationId)
        {
            return accountsById.TryGetValue(destinationId, out var destination)
                ? string.Format(Translator.Get("Transactions_TransferToFormat"), destination.Name)
                : Translator.Get("Enum_TransactionType_Transfer");
        }

        if (transaction.CategoryId is { } categoryId && categoriesById.TryGetValue(categoryId, out var category))
        {
            return category.Name;
        }

        return Translator.Get("Transactions_OtherCategoryFallback");
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
        SelectionSummaryText = Translator.GetPlural("Common_SelectionCount", count);
    }

    private void ToggleFilterPanel()
    {
        if (FilterOpen)
        {
            FilterOpen = false;
            return;
        }

        CloseAllPanels();
        FilterOpen = true;
    }

    private void ToggleAddMenuPanel()
    {
        if (AddMenuOpen)
        {
            AddMenuOpen = false;
            return;
        }

        CloseAllPanels();
        AddMenuOpen = true;
    }

    private void OpenAddForm(TransactionType type)
    {
        CloseAllPanels();
        _ = AddForm.OpenAsync(type);
    }

    private async Task OpenScheduleFormAsync()
    {
        CloseAllPanels();
        await ScheduleForm.OpenAsync();
    }

    private async Task OpenScheduleFormForEditAsync(ScheduledTransaction schedule)
    {
        CloseAllPanels();
        await ScheduleForm.OpenForEditAsync(schedule);
    }

    private void CloseAllPanels()
    {
        FilterOpen = false;
        AddMenuOpen = false;
        AddForm.Close();
        ScheduleForm.Close();
    }

    private async Task DeleteSelectedAsync()
    {
        var selectedIds = Rows.Where(row => row.IsSelected).Select(row => row.Id).ToList();
        foreach (var id in selectedIds)
        {
            await transactionRepository.DeleteAsync(id);
        }

        // Touches UI-bound state after an await that may have resumed off the UI thread (see
        // ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() => SelectMode = false);
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

        // Touches UI-bound state after an await that may have resumed off the UI thread (see
        // ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() => SelectMode = false);
        await InitializeAsync();
    }
}
