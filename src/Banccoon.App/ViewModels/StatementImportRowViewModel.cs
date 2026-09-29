using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Categories;
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
    private Guid? suggestedCategoryId;
    private TransactionType suggestedType;
    private NamedOptionViewModel? suggestedOtherAccount;

    public StatementImportRowViewModel(
        StatementImportRow row,
        string currency,
        ObservableCollection<CategoryOptionViewModel> categoryOptions,
        CategoryTree categoryTree,
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
        BankCategoryText = string.IsNullOrWhiteSpace(row.BankCategory)
            ? string.Empty
            : string.Format(Translator.Get("StatementImport_BankCategoryFormat"), row.BankCategory);
        CategoryOptions = categoryOptions;
        Subcategory = new SubcategoryPickerViewModel();
        Subcategory.Reset(categoryTree);
        OtherAccountOptions = otherAccountOptions;
        type = row.Type;

        // No silent defaults: a row with nothing learned starts with no category (approving it
        // as-is files it under "Other", per the Phase 3 spec) and a transfer with no learned other
        // account starts with none picked - rather than quietly preselecting whichever category or
        // account happens to sort first, which looked like a real suggestion but wasn't.
        // A child category shows as its parent in the picker, with the child next to it.
        var selectedCategoryId = row.CategoryId ?? row.SuggestedCategoryId;
        category = selectedCategoryId is { } categoryId
            ? CategoryOptionsHelper.FindParentOption(categoryOptions, categoryTree, categoryId)
            : null;
        if (category is not null)
        {
            Subcategory.SelectCategory(selectedCategoryId!.Value);
        }

        otherAccount = row.DestinationAccountId is { } otherAccountId
            ? otherAccountOptions.FirstOrDefault(option => option.Id == otherAccountId)
            : null;

        suggestedCategoryId = CategoryId;
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
    public decimal SignedAmount => Type == TransactionType.Transfer
        ? (isIncoming ? amount : -amount)
        : MoneyFlow.GetSignedAmount(amount, Type);

    public string AmountText => MoneyFormat.Format(SignedAmount, currency);

    public bool IsDuplicate { get; }

    // The bank's own category for the operation, shown small under the description - a hint when
    // deciding a row's category (see BankCategoryLink).
    public string BankCategoryText { get; }

    public bool HasBankCategory => BankCategoryText.Length > 0;

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

    // The chosen parent's children, when it has any (see SubcategoryPickerViewModel).
    public SubcategoryPickerViewModel Subcategory { get; }

    // The existing category the row is filed under: the chosen child, else the chosen parent.
    // Null for no category, or a "+ New category" not created yet.
    public Guid? CategoryId => Category is { IsCategory: true } option ? Subcategory.Resolve(option.Id) : null;

    // Bound two-way to the row's category picker. A picker never offers "no category", so a null
    // arriving here is MAUI resetting the picker while it builds the row's view - seen 2026-09-25
    // when opening a group: every row in it lost its category, and the group's "Approve all" then
    // skipped them all as no longer ready. The category is kept and handed back to the picker once
    // it's done. Code that really means "no category" uses RestoreCategory.
    public CategoryOptionViewModel? Category
    {
        get => category;
        set
        {
            if (value is null && category is not null)
            {
                _ = ReassertCategoryAsync();
                return;
            }

            SetCategory(value);
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
        && CategoryId == suggestedCategoryId
        && Type == suggestedType
        && OtherAccount?.Id == suggestedOtherAccount?.Id;

    // Sets the category from code, where "none" is a real value - a newer suggestion with nothing
    // learned, or putting a pick back after the option list changed (StatementImportCategoriesViewModel.AddOption).
    public void RestoreCategory(CategoryOptionViewModel? value)
    {
        SetCategory(value);
    }

    // Fills an untouched row with a newer suggestion and re-decides its block. Returns whether the
    // block changed. UI-thread only.
    public bool ApplySuggestion(TransactionType newType, Guid? newCategoryId, NamedOptionViewModel? newOtherAccount)
    {
        suggestedType = newType;
        suggestedOtherAccount = newOtherAccount;
        Type = newType;
        SetCategoryById(newCategoryId);
        suggestedCategoryId = CategoryId;
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

    // Sets the category from a stored id: a parent goes in the picker, a child goes in the picker
    // as its parent with the child chosen next to it. An id not in the list (or null) means no
    // category. UI-thread only.
    public void SetCategoryById(Guid? categoryId)
    {
        var option = categoryId is { } id ? CategoryOptionsHelper.FindParentOption(CategoryOptions, Subcategory.Tree, id) : null;
        SetCategory(option);
        if (option is not null)
        {
            Subcategory.SelectCategory(categoryId!.Value);
        }
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

    private void SetCategory(CategoryOptionViewModel? value)
    {
        if (SetProperty(ref category, value))
        {
            OnPropertyChanged(nameof(IsCreatingNewCategory));
            OnPropertyChanged(nameof(CategoryBorderColor));
            OnPropertyChanged(nameof(IsReadyToApprove));
            Subcategory.ShowChildrenOf(value is { IsCategory: true } ? value.Id : null);
        }
    }

    // After the picker has finished whatever reset it, tell it the category again.
    private async Task ReassertCategoryAsync()
    {
        await Task.Yield();
        await RunOnMainThreadAsync(() => OnPropertyChanged(nameof(Category)));
    }

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
