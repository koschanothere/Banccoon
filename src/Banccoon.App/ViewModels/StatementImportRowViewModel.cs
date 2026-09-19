using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

public sealed class StatementImportRowViewModel : ViewModelBase
{
    private readonly decimal amount;
    private readonly string currency;

    private NamedOptionViewModel? category;
    private TransactionType type;
    private bool isSelected;
    private bool isSelectModeActive;

    public StatementImportRowViewModel(
        StatementImportRow row,
        string currency,
        ObservableCollection<NamedOptionViewModel> categoryOptions,
        Func<StatementImportRowViewModel, Task> onApprove,
        Func<StatementImportRowViewModel, Task> onSkip)
    {
        Id = row.Id;
        amount = row.Amount;
        this.currency = currency;
        DateText = row.Date.ToString("dd/MM/yyyy");
        Description = string.IsNullOrWhiteSpace(row.Counterparty) ? row.Description : row.Counterparty;
        IsDuplicate = row.IsDuplicate;
        CategoryOptions = categoryOptions;
        type = row.Type;

        var selectedCategoryId = row.CategoryId ?? row.SuggestedCategoryId;
        category = selectedCategoryId is { } categoryId
            ? categoryOptions.FirstOrDefault(option => option.Id == categoryId)
            : categoryOptions.FirstOrDefault();

        ApproveCommand = new RelayCommand(() => _ = onApprove(this));
        SkipCommand = new RelayCommand(() => _ = onSkip(this));
        SetExpenseCommand = new RelayCommand(() => Type = TransactionType.Expense);
        SetIncomeCommand = new RelayCommand(() => Type = TransactionType.Income);
    }

    public Guid Id { get; }

    public string DateText { get; }

    public string Description { get; }

    // Derived from Type rather than fixed at construction, since the type toggle below can flip
    // it (e.g. a "Перевод" guessed as Expense corrected to Income) - the sign has to follow.
    public string AmountText => MoneyFormat.Format(MoneyFlow.GetSignedAmount(amount, Type), currency);

    public bool IsDuplicate { get; }

    public ObservableCollection<NamedOptionViewModel> CategoryOptions { get; }

    public NamedOptionViewModel? Category
    {
        get => category;
        set => SetProperty(ref category, value);
    }

    public TransactionType Type
    {
        get => type;
        set
        {
            if (SetProperty(ref type, value))
            {
                OnPropertyChanged(nameof(IsExpenseType));
                OnPropertyChanged(nameof(IsIncomeType));
                OnPropertyChanged(nameof(AmountText));
            }
        }
    }

    public bool IsExpenseType => Type == TransactionType.Expense;

    public bool IsIncomeType => Type == TransactionType.Income;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool IsSelectModeActive
    {
        get => isSelectModeActive;
        set => SetProperty(ref isSelectModeActive, value);
    }

    public ICommand ApproveCommand { get; }

    public ICommand SkipCommand { get; }

    public ICommand SetExpenseCommand { get; }

    public ICommand SetIncomeCommand { get; }
}
