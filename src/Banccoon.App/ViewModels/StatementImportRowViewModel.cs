using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

public sealed class StatementImportRowViewModel : ViewModelBase
{
    private readonly decimal amount;
    private readonly string currency;
    private readonly bool isIncoming;

    private CategoryOptionViewModel? category;
    private string newCategoryName = string.Empty;
    private TransactionType type;
    private NamedOptionViewModel? otherAccount;
    private bool isSelected;
    private bool isSelectModeActive;

    public StatementImportRowViewModel(
        StatementImportRow row,
        string currency,
        ObservableCollection<CategoryOptionViewModel> categoryOptions,
        ObservableCollection<NamedOptionViewModel> otherAccountOptions,
        Func<StatementImportRowViewModel, Task> onApprove,
        Func<StatementImportRowViewModel, Task> onSkip)
    {
        Id = row.Id;
        amount = row.Amount;
        this.currency = currency;
        isIncoming = row.IsIncoming;
        DateText = row.Date.ToString("dd/MM/yyyy");
        Description = string.IsNullOrWhiteSpace(row.Counterparty) ? row.Description : row.Counterparty;
        IsDuplicate = row.IsDuplicate;
        CategoryOptions = categoryOptions;
        OtherAccountOptions = otherAccountOptions;
        type = row.Type;

        var selectedCategoryId = row.CategoryId ?? row.SuggestedCategoryId;
        category = selectedCategoryId is { } categoryId
            ? categoryOptions.FirstOrDefault(option => option.Id == categoryId && !option.IsCreateNew)
            : categoryOptions.FirstOrDefault(option => !option.IsCreateNew);

        otherAccount = row.DestinationAccountId is { } otherAccountId
            ? otherAccountOptions.FirstOrDefault(option => option.Id == otherAccountId)
            : otherAccountOptions.FirstOrDefault();

        ApproveCommand = new RelayCommand(() => _ = onApprove(this));
        SkipCommand = new RelayCommand(() => _ = onSkip(this));
    }

    public IReadOnlyList<TransactionType> TypeOptions { get; } = Enum.GetValues<TransactionType>();

    public Guid Id { get; }

    public string DateText { get; }

    public string Description { get; }

    // Expense/Income get their sign from Type directly (that's what those words mean). A Transfer
    // doesn't inherently say which way money moved, so it falls back to the original statement's
    // own +/- sign, captured once and preserved regardless of how Type gets reclassified.
    public string AmountText => MoneyFormat.Format(
        Type == TransactionType.Transfer
            ? (isIncoming ? amount : -amount)
            : MoneyFlow.GetSignedAmount(amount, Type),
        currency);

    public bool IsDuplicate { get; }

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

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

    // Falls back to transparent so a row never shows a stray colored border before any category
    // (or the create-new sentinel) has a color to show.
    public Color CategoryBorderColor => Category?.Color ?? Colors.Transparent;

    public string NewCategoryName
    {
        get => newCategoryName;
        set => SetProperty(ref newCategoryName, value);
    }

    // The other account on a transfer - not always the semantic "destination" (see
    // StatementImportRow.DestinationAccountId); the label in the review row is deliberately
    // direction-neutral for this reason.
    public ObservableCollection<NamedOptionViewModel> OtherAccountOptions { get; }

    public NamedOptionViewModel? OtherAccount
    {
        get => otherAccount;
        set => SetProperty(ref otherAccount, value);
    }

    public TransactionType Type
    {
        get => type;
        set
        {
            if (SetProperty(ref type, value))
            {
                OnPropertyChanged(nameof(IsTransferType));
                OnPropertyChanged(nameof(AmountText));
            }
        }
    }

    public bool IsTransferType => Type == TransactionType.Transfer;

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
}
