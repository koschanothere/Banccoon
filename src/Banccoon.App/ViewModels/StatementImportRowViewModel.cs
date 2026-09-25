using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

// Which block of the review list a row is shown in - decided when the row is loaded, and changed
// only when a newer suggestion fills in a row the user hasn't touched (ApplySuggestion), so rows
// never jump between blocks while the user is editing them.
public enum StatementImportRowSection
{
    // Possibly already recorded (StatementImportRow.IsDuplicate): usually just skipped.
    Duplicate,

    // No learned category, or a transfer with no learned other account: needs a decision.
    Attention,

    // Category (and, for a transfer, the other account) already came from what Banccoon learned.
    Ready
}

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
    private bool isBusy;
    private bool isTypeEditorOpen;

    // What Banccoon last suggested for this row (on load, or after a later approval taught it more)
    // - see IsUntouched.
    private CategoryOptionViewModel? suggestedCategory;
    private TransactionType suggestedType;
    private NamedOptionViewModel? suggestedOtherAccount;

    public StatementImportRowViewModel(
        StatementImportRow row,
        string currency,
        ObservableCollection<CategoryOptionViewModel> categoryOptions,
        ObservableCollection<NamedOptionViewModel> otherAccountOptions,
        Func<StatementImportRowViewModel, Task> onApprove,
        Func<StatementImportRowViewModel, Task> onSkip,
        Func<StatementImportRowViewModel, Task> onCreateCategory)
    {
        Id = row.Id;
        Date = row.Date;
        amount = row.Amount;
        this.currency = currency;
        isIncoming = row.IsIncoming;
        DateText = row.Date.ToString("dd/MM/yyyy");
        Description = string.IsNullOrWhiteSpace(row.Counterparty) ? row.Description : row.Counterparty;
        IsDuplicate = row.IsDuplicate;
        CategoryOptions = categoryOptions;
        OtherAccountOptions = otherAccountOptions;
        type = row.Type;

        // No silent defaults: a row with nothing learned starts with no category (approving it
        // as-is files it under "Other", per the Phase 3 spec) and a transfer with no learned other
        // account starts with none picked - rather than quietly preselecting whichever category or
        // account happens to sort first, which looked like a real suggestion but wasn't.
        var selectedCategoryId = row.CategoryId ?? row.SuggestedCategoryId;
        category = selectedCategoryId is { } categoryId
            ? categoryOptions.FirstOrDefault(option => option.Id == categoryId && !option.IsCreateNew)
            : null;

        otherAccount = row.DestinationAccountId is { } otherAccountId
            ? otherAccountOptions.FirstOrDefault(option => option.Id == otherAccountId)
            : null;

        suggestedCategory = category;
        suggestedType = type;
        suggestedOtherAccount = otherAccount;
        Section = CurrentSection();

        ApproveCommand = new RelayCommand(() => _ = onApprove(this));
        SkipCommand = new RelayCommand(() => _ = onSkip(this));
        CreateCategoryCommand = new RelayCommand(() => _ = onCreateCategory(this));
        ToggleTypeEditorCommand = new RelayCommand(() => IsTypeEditorOpen = !IsTypeEditorOpen);
    }

    public IReadOnlyList<TransactionType> TypeOptions { get; } = Enum.GetValues<TransactionType>();

    public Guid Id { get; }

    public DateOnly Date { get; }

    public StatementImportRowSection Section { get; private set; }

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
                OnPropertyChanged(nameof(IsReadyToApprove));
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
        set
        {
            if (SetProperty(ref newCategoryName, value))
            {
                OnPropertyChanged(nameof(IsReadyToApprove));
            }
        }
    }

    // The other account on a transfer - not always the semantic "destination" (see
    // StatementImportRow.DestinationAccountId); the label in the review row is deliberately
    // direction-neutral for this reason.
    public ObservableCollection<NamedOptionViewModel> OtherAccountOptions { get; }

    public NamedOptionViewModel? OtherAccount
    {
        get => otherAccount;
        set
        {
            if (SetProperty(ref otherAccount, value))
            {
                OnPropertyChanged(nameof(IsReadyToApprove));
            }
        }
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
                OnPropertyChanged(nameof(TypeToolTipText));
                OnPropertyChanged(nameof(IsReadyToApprove));
                IsTypeEditorOpen = false;
            }
        }
    }

    public bool IsTransferType => Type == TransactionType.Transfer;

    // "Calmer rows": the type picker stays hidden and the amount's colour shows the detected type
    // (TransactionTypeColorConverter: income green, transfer purple, expense plain). Clicking the
    // amount opens the picker; choosing a type closes it again.
    public bool IsTypeEditorOpen
    {
        get => isTypeEditorOpen;
        set => SetProperty(ref isTypeEditorOpen, value);
    }

    public string TypeToolTipText => string.Format(Translator.Get("StatementImport_TypeTooltipFormat"), DisplayText.Format(Type));

    // Can be approved as it stands, with no decision left to make: has a category (a "create new"
    // one counts once it's named), and a transfer has its other account. Duplicates never count -
    // they're for skipping unless the user approves one explicitly.
    public bool IsReadyToApprove =>
        !IsDuplicate
        && Category is not null
        && (!Category.IsCreateNew || !string.IsNullOrWhiteSpace(NewCategoryName))
        && (!IsTransferType || OtherAccount is not null);

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

    // True while an approve/skip for this row is queued or in flight - disables its buttons so a
    // double-click can't start a second approve of the same row (see
    // StatementImportReviewViewModel.TryBeginRowActionAsync). UI-thread only, like every other
    // bound property here.
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
            }
        }
    }

    public bool IsIdle => !IsBusy;

    public bool TryBeginAction()
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        return true;
    }

    public void EndAction()
    {
        IsBusy = false;
    }

    // Nothing here differs from what Banccoon last suggested for the row (including "nothing yet"):
    // the user hasn't picked a category, type or other account of their own, so a newer suggestion
    // can safely replace it.
    public bool IsUntouched =>
        !IsCreatingNewCategory
        && Category?.Id == suggestedCategory?.Id
        && Type == suggestedType
        && OtherAccount?.Id == suggestedOtherAccount?.Id;

    // Fills an untouched row with a newer suggestion and re-decides its block. Returns whether the
    // block changed. UI-thread only.
    public bool ApplySuggestion(TransactionType newType, CategoryOptionViewModel? newCategory, NamedOptionViewModel? newOtherAccount)
    {
        suggestedType = newType;
        suggestedCategory = newCategory;
        suggestedOtherAccount = newOtherAccount;
        Type = newType;
        Category = newCategory;
        OtherAccount = newOtherAccount;

        var section = CurrentSection();
        if (section == Section)
        {
            return false;
        }

        Section = section;
        return true;
    }

    // Before an undone row goes back into the list: keep its picks, drop transient UI state.
    public void PrepareForReinsert()
    {
        IsSelected = false;
        IsTypeEditorOpen = false;
        IsBusy = false;
    }

    // Points a row that was about to create a category at the real one with that name - created by
    // this row's own Add/Enter, or by another row that typed the same name first.
    public void AdoptCategory(CategoryOptionViewModel option)
    {
        Category = option;
        NewCategoryName = string.Empty;
    }

    public ICommand ApproveCommand { get; }

    // Creates the category named in NewCategoryName right away (Enter in the name box, or its Add
    // button), so every other row can pick it too - see StatementImportCategoriesViewModel.
    public ICommand CreateCategoryCommand { get; }

    public ICommand ToggleTypeEditorCommand { get; }

    public ICommand SkipCommand { get; }

    private StatementImportRowSection CurrentSection()
    {
        if (IsDuplicate)
        {
            return StatementImportRowSection.Duplicate;
        }

        return Category is not null && (!IsTransferType || OtherAccount is not null)
            ? StatementImportRowSection.Ready
            : StatementImportRowSection.Attention;
    }
}
