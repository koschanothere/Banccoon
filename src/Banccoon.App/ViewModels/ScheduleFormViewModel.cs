using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.App.Localization;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Repositories;
using Microsoft.Maui.Graphics;

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
    private Guid? editingScheduledTransactionId;
    private TransactionType type = TransactionType.Expense;
    private string name = string.Empty;
    private NamedOptionViewModel? account;
    private string amountText = string.Empty;
    private CategoryOptionViewModel? category;
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
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        DeleteCommand = new RelayCommand(() => _ = DeleteAsync());
    }

    public bool IsOpen
    {
        get => isOpen;
        private set => SetProperty(ref isOpen, value);
    }

    public bool IsEditing => editingScheduledTransactionId.HasValue;

    public string FormTitle => IsEditing
        ? Translator.Get("Schedule_EditRuleTitle")
        : Translator.Get("Schedule_NewRuleTitle");

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

    public RecurrenceEditorViewModel Recurrence
    {
        get => recurrence;
        private set => SetProperty(ref recurrence, value);
    }

    public ObservableCollection<NamedOptionViewModel> AccountOptions { get; }

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

    public ICommand SetExpenseCommand { get; }

    public ICommand SetIncomeCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand DeleteCommand { get; }

    public void Close()
    {
        IsOpen = false;
    }

    public Task OpenAsync(CancellationToken cancellationToken = default) => OpenInternalAsync(existing: null, cancellationToken);

    public Task OpenForEditAsync(ScheduledTransaction existing, CancellationToken cancellationToken = default) => OpenInternalAsync(existing, cancellationToken);

    private async Task OpenInternalAsync(ScheduledTransaction? existing, CancellationToken cancellationToken)
    {
        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        var categories = await categoryRepository.GetAllAsync(cancellationToken);

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

            editingScheduledTransactionId = existing?.Id;
            Name = existing?.Name ?? string.Empty;
            Type = existing?.Type ?? TransactionType.Expense;
            Account = existing is null
                ? AccountOptions.FirstOrDefault()
                : AccountOptions.FirstOrDefault(option => option.Id == existing.AccountId) ?? AccountOptions.FirstOrDefault();
            AmountText = existing is null ? string.Empty : existing.Amount.ToString(CultureInfo.InvariantCulture);
            Category = existing?.CategoryId is { } categoryId
                ? CategoryOptions.FirstOrDefault(option => option.Id == categoryId && !option.IsCreateNew)
                : CategoryOptions.FirstOrDefault(option => !option.IsCreateNew);
            NewCategoryName = string.Empty;
            StatusText = string.Empty;
            Recurrence = CreateRecurrenceEditor();
            if (existing is not null)
            {
                Recurrence.ApplyRule(existing.RecurrenceRule);
            }

            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(FormTitle));
            IsOpen = true;
        });
    }

    private RecurrenceEditorViewModel CreateRecurrenceEditor()
    {
        return new RecurrenceEditorViewModel(
            dateProvider,
            recurrenceDescriptionService,
            recurrenceSyntaxService,
            recurrenceValidationService);
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

        if (!Recurrence.IsValid)
        {
            StatusText = string.IsNullOrEmpty(Recurrence.ValidationMessage)
                ? Translator.Get("Schedule_FixRecurrenceRule")
                : Recurrence.ValidationMessage;
            return;
        }

        if (Category?.IsCreateNew == true && string.IsNullOrWhiteSpace(NewCategoryName))
        {
            StatusText = Translator.Get("Common_NameNewCategoryFirst");
            return;
        }

        var (categoryId, newOption) = await CategoryOptionsHelper.ResolveOrCreateAsync(Category, NewCategoryName, categoryRepository);
        if (newOption is not null)
        {
            await RunOnMainThreadAsync(() => CategoryOptionsHelper.InsertBeforeSentinel(CategoryOptions, newOption));
        }

        var rule = Recurrence.BuildRule();
        var scheduledTransaction = new ScheduledTransaction(
            editingScheduledTransactionId ?? Guid.NewGuid(),
            Name.Trim(),
            amount,
            Account.Id,
            categoryId,
            Type,
            rule,
            rule.StartDate,
            Active: true);

        await scheduledTransactionRepository.SaveAsync(scheduledTransaction);

        // Touches UI-bound state after an await that may have resumed off the UI thread (see
        // ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() => IsOpen = false);
        await onSaved();
    }

    private async Task DeleteAsync()
    {
        if (editingScheduledTransactionId is not { } id)
        {
            return;
        }

        await scheduledTransactionRepository.DeleteAsync(id);

        await RunOnMainThreadAsync(() => IsOpen = false);
        await onSaved();
    }
}
