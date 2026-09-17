using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class ScheduleFormViewModel : ViewModelBase
{
    private readonly IDateProvider dateProvider;
    private readonly IAccountRepository accountRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly IRecurrenceDescriptionService recurrenceDescriptionService;
    private readonly IRecurrenceSyntaxService recurrenceSyntaxService;
    private readonly IRecurrenceValidationService recurrenceValidationService;
    private readonly Func<Task> onSaved;

    private bool isOpen;
    private TransactionType type = TransactionType.Expense;
    private string name = string.Empty;
    private NamedOptionViewModel? account;
    private string amountText = string.Empty;
    private NamedOptionViewModel? category;
    private bool isAddingCategory;
    private string newCategoryName = string.Empty;
    private string statusText = string.Empty;
    private RecurrenceEditorViewModel recurrence;

    public ScheduleFormViewModel(
        IDateProvider dateProvider,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        IRecurrenceDescriptionService recurrenceDescriptionService,
        IRecurrenceSyntaxService recurrenceSyntaxService,
        IRecurrenceValidationService recurrenceValidationService,
        Func<Task> onSaved)
    {
        this.dateProvider = dateProvider;
        this.accountRepository = accountRepository;
        this.categoryRepository = categoryRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.recurrenceDescriptionService = recurrenceDescriptionService;
        this.recurrenceSyntaxService = recurrenceSyntaxService;
        this.recurrenceValidationService = recurrenceValidationService;
        this.onSaved = onSaved;

        AccountOptions = [];
        CategoryOptions = [];
        recurrence = CreateRecurrenceEditor();

        SetExpenseCommand = new RelayCommand(() => Type = TransactionType.Expense);
        SetIncomeCommand = new RelayCommand(() => Type = TransactionType.Income);
        CloseCommand = new RelayCommand(Close);
        ToggleAddCategoryCommand = new RelayCommand(() => IsAddingCategory = !IsAddingCategory);
        CreateCategoryCommand = new RelayCommand(() => _ = CreateCategoryAsync());
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
    }

    public bool IsOpen
    {
        get => isOpen;
        private set => SetProperty(ref isOpen, value);
    }

    public TransactionType Type
    {
        get => type;
        private set
        {
            if (SetProperty(ref type, value))
            {
                OnPropertyChanged(nameof(IsExpenseType));
                OnPropertyChanged(nameof(IsIncomeType));
            }
        }
    }

    public bool IsExpenseType => Type == TransactionType.Expense;

    public bool IsIncomeType => Type == TransactionType.Income;

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

    public RecurrenceEditorViewModel Recurrence
    {
        get => recurrence;
        private set => SetProperty(ref recurrence, value);
    }

    public ObservableCollection<NamedOptionViewModel> AccountOptions { get; }

    public ObservableCollection<NamedOptionViewModel> CategoryOptions { get; }

    public ICommand SetExpenseCommand { get; }

    public ICommand SetIncomeCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand ToggleAddCategoryCommand { get; }

    public ICommand CreateCategoryCommand { get; }

    public ICommand SaveCommand { get; }

    public void Close()
    {
        IsOpen = false;
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
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
        Type = TransactionType.Expense;
        Account = AccountOptions.FirstOrDefault();
        AmountText = string.Empty;
        Category = CategoryOptions.FirstOrDefault();
        IsAddingCategory = false;
        NewCategoryName = string.Empty;
        StatusText = string.Empty;
        Recurrence = CreateRecurrenceEditor();
        IsOpen = true;
    }

    private RecurrenceEditorViewModel CreateRecurrenceEditor()
    {
        return new RecurrenceEditorViewModel(
            dateProvider,
            recurrenceDescriptionService,
            recurrenceSyntaxService,
            recurrenceValidationService);
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

        if (!Recurrence.IsValid)
        {
            StatusText = string.IsNullOrEmpty(Recurrence.ValidationMessage)
                ? "Fix the recurrence rule."
                : Recurrence.ValidationMessage;
            return;
        }

        var rule = Recurrence.BuildRule();
        var scheduledTransaction = new ScheduledTransaction(
            Guid.NewGuid(),
            Name.Trim(),
            amount,
            Account.Id,
            Category?.Id,
            Type,
            rule,
            rule.StartDate,
            Active: true);

        await scheduledTransactionRepository.SaveAsync(scheduledTransaction);

        IsOpen = false;
        await onSaved();
    }
}
