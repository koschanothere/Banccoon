using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.App.Localization;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

public sealed class NewTransactionFormViewModel : ViewModelBase
{
    private readonly IAccountRepository accountRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly ITransactionApplicationService transactionApplicationService;
    private readonly ISettingsRepository settingsRepository;
    private readonly Func<Task> onSaved;

    private TransactionType type;
    private bool isOpen;
    private string title = string.Empty;
    private string name = string.Empty;
    private NamedOptionViewModel? account;
    private NamedOptionViewModel? destinationAccount;
    private DateTime date = DateTime.Today;
    private string amountText = string.Empty;
    private CategoryOptionViewModel? category;
    private string newCategoryName = string.Empty;
    private string statusText = string.Empty;

    public NewTransactionFormViewModel(
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        ITransactionApplicationService transactionApplicationService,
        ISettingsRepository settingsRepository,
        Func<Task> onSaved)
    {
        this.accountRepository = accountRepository;
        this.categoryRepository = categoryRepository;
        this.transactionRepository = transactionRepository;
        this.transactionApplicationService = transactionApplicationService;
        this.settingsRepository = settingsRepository;
        this.onSaved = onSaved;

        AccountOptions = [];
        CategoryOptions = [];

        CloseCommand = new RelayCommand(Close);
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
    }

    public bool IsOpen
    {
        get => isOpen;
        private set => SetProperty(ref isOpen, value);
    }

    public string Title
    {
        get => title;
        private set => SetProperty(ref title, value);
    }

    public bool IsTransferForm => type == TransactionType.Transfer;

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public NamedOptionViewModel? Account
    {
        get => account;
        set => SetProperty(ref account, value);
    }

    public NamedOptionViewModel? DestinationAccount
    {
        get => destinationAccount;
        set => SetProperty(ref destinationAccount, value);
    }

    public DateTime Date
    {
        get => date;
        set => SetProperty(ref date, value);
    }

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
                OnPropertyChanged(nameof(CategoryBorderColor));
            }
        }
    }

    public bool IsCreatingNewCategory => Category?.IsCreateNew == true;

    public Color CategoryBorderColor => Category?.Color ?? Colors.Transparent;

    public string NewCategoryName
    {
        get => newCategoryName;
        set => SetProperty(ref newCategoryName, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ObservableCollection<NamedOptionViewModel> AccountOptions { get; }

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

    public ICommand CloseCommand { get; }

    public ICommand SaveCommand { get; }

    public void Close()
    {
        IsOpen = false;
    }

    public async Task OpenAsync(TransactionType openType, CancellationToken cancellationToken = default)
    {
        type = openType;
        Title = openType switch
        {
            TransactionType.Expense => Translator.Get("NewTransaction_ExpenseTitle"),
            TransactionType.Income => Translator.Get("NewTransaction_IncomeTitle"),
            _ => Translator.Get("NewTransaction_TransferTitle")
        };

        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var settings = await settingsRepository.GetAsync(cancellationToken);

        // Everything below touches UI-bound state, and the awaits above may have resumed off the
        // UI thread (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            AccountOptions.Clear();
            foreach (var accountItem in accounts.Where(accountItem => !accountItem.IsArchived).OrderBy(accountItem => accountItem.Name))
            {
                AccountOptions.Add(new NamedOptionViewModel(accountItem.Id, accountItem.Name));
            }

            CategoryOptionsHelper.Repopulate(CategoryOptions, categories);

            Name = string.Empty;
            Account = (settings.PrimaryAccountId is { } primaryAccountId
                ? AccountOptions.FirstOrDefault(option => option.Id == primaryAccountId)
                : null) ?? AccountOptions.FirstOrDefault();
            DestinationAccount = AccountOptions.Skip(1).FirstOrDefault() ?? Account;
            Date = DateTime.Today;
            AmountText = string.Empty;
            Category = CategoryOptions.FirstOrDefault(option => !option.IsCreateNew);
            NewCategoryName = string.Empty;
            StatusText = string.Empty;
            OnPropertyChanged(nameof(IsTransferForm));
            IsOpen = true;
        });
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusText = Translator.Get("Common_NameRequired");
            return;
        }

        if (Account is null)
        {
            StatusText = Translator.Get("Common_ChooseAnAccount");
            return;
        }

        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0m)
        {
            StatusText = Translator.Get("Common_AmountMustBePositive");
            return;
        }

        if (IsTransferForm && (DestinationAccount is null || DestinationAccount.Id == Account.Id))
        {
            StatusText = Translator.Get("NewTransaction_ChooseDifferentDestination");
            return;
        }

        if (!IsTransferForm && Category?.IsCreateNew == true && string.IsNullOrWhiteSpace(NewCategoryName))
        {
            StatusText = Translator.Get("Common_NameNewCategoryFirst");
            return;
        }

        Guid? categoryId = null;
        if (!IsTransferForm)
        {
            var (resolvedCategoryId, newOption) = await CategoryOptionsHelper.ResolveOrCreateAsync(Category, NewCategoryName, categoryRepository);
            if (newOption is not null)
            {
                await RunOnMainThreadAsync(() => CategoryOptionsHelper.InsertBeforeSentinel(CategoryOptions, newOption));
            }

            categoryId = resolvedCategoryId;
        }

        var transaction = new Transaction(
            Guid.NewGuid(),
            DateOnly.FromDateTime(Date),
            amount,
            Account.Id,
            categoryId,
            Notes: null,
            type,
            DestinationAccountId: IsTransferForm ? DestinationAccount!.Id : null,
            Name: Name.Trim());

        var accounts = await accountRepository.GetAllAsync();
        var accountsById = accounts.ToDictionary(accountItem => accountItem.Id);
        var updatedAccounts = transactionApplicationService.ApplyNewTransaction(transaction, accountsById);

        await transactionRepository.SaveAsync(transaction);
        foreach (var updatedAccount in updatedAccounts)
        {
            await accountRepository.SaveAsync(updatedAccount);
        }

        // Touches UI-bound state after an await that may have resumed off the UI thread (see
        // ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() => IsOpen = false);
        await onSaved();
    }
}
