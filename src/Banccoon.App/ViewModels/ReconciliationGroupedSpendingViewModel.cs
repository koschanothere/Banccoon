using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Reconciliation;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;

namespace Banccoon.App.ViewModels;

// Reconciliation step 3 ("explain the difference"): records spending that happened but was never
// entered - typically cash, or a week of small card purchases - as one expense per group, via the
// existing IGroupedSpendingService, so the gap shrinks with real, categorized transactions before
// anything is written off as a bare balance adjustment.
public sealed class ReconciliationGroupedSpendingViewModel : ViewModelBase
{
    private readonly ICategoryRepository categoryRepository;
    private readonly IAccountRepository accountRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IGroupedSpendingService groupedSpendingService;
    private readonly ITransactionApplicationService transactionApplicationService;
    private readonly Func<Task> onAdded;

    private Guid accountId;
    private string currency = "EUR";
    private DateOnly date;
    private string amountText = string.Empty;
    private CategoryOptionViewModel? category;
    private string newCategoryName = string.Empty;
    private string noteText = string.Empty;
    private string statusText = string.Empty;
    private bool isSaving;

    public ReconciliationGroupedSpendingViewModel(
        ICategoryRepository categoryRepository,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IGroupedSpendingService groupedSpendingService,
        ITransactionApplicationService transactionApplicationService,
        Func<Task> onAdded)
    {
        this.categoryRepository = categoryRepository;
        this.accountRepository = accountRepository;
        this.transactionRepository = transactionRepository;
        this.groupedSpendingService = groupedSpendingService;
        this.transactionApplicationService = transactionApplicationService;
        this.onAdded = onAdded;

        CategoryOptions = [];
        AddedEntries = [];
        AddCommand = new RelayCommand(() => _ = AddAsync());
    }

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

    // One line per group recorded during this check-in (Id = the created transaction), e.g.
    // "Groceries (cash): RUB -2,400.00".
    public ObservableCollection<NamedOptionViewModel> AddedEntries { get; }

    public string AmountText
    {
        get => amountText;
        set => SetProperty(ref amountText, value);
    }

    public CategoryOptionViewModel? Category
    {
        get => category;
        set
        {
            if (SetProperty(ref category, value))
            {
                OnPropertyChanged(nameof(IsCreatingNewCategory));
            }
        }
    }

    public bool IsCreatingNewCategory => Category?.IsCreateNew == true;

    public string NewCategoryName
    {
        get => newCategoryName;
        set => SetProperty(ref newCategoryName, value);
    }

    public string NoteText
    {
        get => noteText;
        set => SetProperty(ref noteText, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ICommand AddCommand { get; }

    public async Task LoadAsync(Guid account, string currencyCode, DateOnly checkInDate, CancellationToken cancellationToken = default)
    {
        accountId = account;
        currency = currencyCode;
        date = checkInDate;

        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        await RunOnMainThreadAsync(() =>
        {
            CategoryOptionsHelper.Repopulate(CategoryOptions, categories);
            AddedEntries.Clear();
            ResetForm();
            StatusText = string.Empty;
        });
    }

    private async Task AddAsync()
    {
        // Command handlers start on the UI thread, so this check-and-set can't interleave with a
        // second click; it stops a double-click from recording the same group twice.
        if (isSaving)
        {
            return;
        }

        isSaving = true;
        try
        {
            if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0m)
            {
                await RunOnMainThreadAsync(() => StatusText = Translator.Get("Common_AmountMustBePositive"));
                return;
            }

            if (IsCreatingNewCategory && string.IsNullOrWhiteSpace(NewCategoryName))
            {
                await RunOnMainThreadAsync(() => StatusText = Translator.Get("Common_NameNewCategoryFirst"));
                return;
            }

            var (categoryId, newOption) = await CategoryOptionsHelper.ResolveOrCreateAsync(Category, NewCategoryName, categoryRepository);
            if (newOption is not null)
            {
                await RunOnMainThreadAsync(() => CategoryOptionsHelper.InsertBeforeSentinel(CategoryOptions, newOption));
            }

            // Name/Notes are written once, in the current UI language, like any other data the
            // user creates - the same treatment BalanceAdjustmentService's Notes text gets.
            var label = string.IsNullOrWhiteSpace(NoteText)
                ? Translator.Get("Reconciliation_GroupedSpendingDefaultName")
                : NoteText.Trim();
            var transaction = groupedSpendingService.CreateTransaction(
                new GroupedSpendingEntry(date, amount, accountId, categoryId, label)) with { Name = label };

            var accounts = await accountRepository.GetAllAsync();
            var updatedAccounts = transactionApplicationService.ApplyNewTransaction(
                transaction,
                accounts.ToDictionary(accountItem => accountItem.Id));
            await transactionRepository.SaveAsync(transaction);
            foreach (var updatedAccount in updatedAccounts)
            {
                await accountRepository.SaveAsync(updatedAccount);
            }

            await RunOnMainThreadAsync(() =>
            {
                AddedEntries.Add(new NamedOptionViewModel(
                    transaction.Id,
                    $"{label}: {MoneyFormat.Format(MoneyFlow.GetSignedAmount(amount, transaction.Type), currency)}"));
                ResetForm();
                StatusText = string.Empty;
            });

            await onAdded();
        }
        finally
        {
            isSaving = false;
        }
    }

    private void ResetForm()
    {
        AmountText = string.Empty;
        // No default category: an unnoticed default would silently mis-file the spending, while
        // leaving it uncategorized is visible and fixable later from Transactions.
        Category = null;
        NewCategoryName = string.Empty;
        NoteText = string.Empty;
    }
}
