using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;

namespace Banccoon.App.ViewModels;

public sealed class NewTransactionFormViewModel : ViewModelBase
{
    private readonly IAccountRepository accountRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly ITransactionApplicationService transactionApplicationService;
    private readonly Func<Task> onSaved;

    private TransactionType type;
    private bool isOpen;
    private string title = string.Empty;
    private string name = string.Empty;
    private NamedOptionViewModel? account;
    private NamedOptionViewModel? destinationAccount;
    private DateTime date = DateTime.Today;
    private string amountText = string.Empty;
    private NamedOptionViewModel? category;
    private bool isAddingCategory;
    private string newCategoryName = string.Empty;
    private string statusText = string.Empty;

    public NewTransactionFormViewModel(
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        ITransactionApplicationService transactionApplicationService,
        Func<Task> onSaved)
    {
        this.accountRepository = accountRepository;
        this.categoryRepository = categoryRepository;
        this.transactionRepository = transactionRepository;
        this.transactionApplicationService = transactionApplicationService;
        this.onSaved = onSaved;

        AccountOptions = [];
        CategoryOptions = [];

        CloseCommand = new RelayCommand(() => IsOpen = false);
        ToggleAddCategoryCommand = new RelayCommand(() => IsAddingCategory = !IsAddingCategory);
        CreateCategoryCommand = new RelayCommand(() => _ = CreateCategoryAsync());
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

    public NamedOptionViewModel? Category
    {
        get => category;
        set => SetProperty(ref category, value);
    }

    public bool IsAddingCategory
    {
        get => isAddingCategory;
        private set => SetProperty(ref isAddingCategory, value);
    }

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

    public ObservableCollection<NamedOptionViewModel> CategoryOptions { get; }

    public ICommand CloseCommand { get; }

    public ICommand ToggleAddCategoryCommand { get; }

    public ICommand CreateCategoryCommand { get; }

    public ICommand SaveCommand { get; }

    public async Task OpenAsync(TransactionType openType, CancellationToken cancellationToken = default)
    {
        type = openType;
        Title = openType switch
        {
            TransactionType.Expense => "New Expense",
            TransactionType.Income => "New Income",
            _ => "New Transfer"
        };

        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        var categories = await categoryRepository.GetAllAsync(cancellationToken);

        AccountOptions.Clear();
        foreach (var accountItem in accounts.Where(accountItem => !accountItem.IsArchived).OrderBy(accountItem => accountItem.Name))
        {
            AccountOptions.Add(new NamedOptionViewModel(accountItem.Id, accountItem.Name));
        }

        CategoryOptions.Clear();
        foreach (var categoryItem in categories.OrderBy(categoryItem => categoryItem.Name))
        {
            CategoryOptions.Add(new NamedOptionViewModel(categoryItem.Id, categoryItem.Name));
        }

        Name = string.Empty;
        Account = AccountOptions.FirstOrDefault();
        DestinationAccount = AccountOptions.Skip(1).FirstOrDefault() ?? Account;
        Date = DateTime.Today;
        AmountText = string.Empty;
        Category = CategoryOptions.FirstOrDefault();
        IsAddingCategory = false;
        NewCategoryName = string.Empty;
        StatusText = string.Empty;
        OnPropertyChanged(nameof(IsTransferForm));
        IsOpen = true;
    }

    private async Task CreateCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            return;
        }

        var newCategory = new Category(Guid.NewGuid(), NewCategoryName.Trim());
        await categoryRepository.SaveAsync(newCategory);

        var option = new NamedOptionViewModel(newCategory.Id, newCategory.Name);
        CategoryOptions.Add(option);
        Category = option;
        IsAddingCategory = false;
        NewCategoryName = string.Empty;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusText = "Name is required.";
            return;
        }

        if (Account is null)
        {
            StatusText = "Choose an account.";
            return;
        }

        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0m)
        {
            StatusText = "Amount must be a positive number.";
            return;
        }

        if (IsTransferForm && (DestinationAccount is null || DestinationAccount.Id == Account.Id))
        {
            StatusText = "Choose a different destination account.";
            return;
        }

        var transaction = new Transaction(
            Guid.NewGuid(),
            DateOnly.FromDateTime(Date),
            amount,
            Account.Id,
            IsTransferForm ? null : Category?.Id,
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

        IsOpen = false;
        await onSaved();
    }
}
