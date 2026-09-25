using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Categories;
using Banccoon.Core.Models;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

// Ready rows that share one sender name and the same category, type and other account - shown in
// the ready block (with "Group by name" on) as one collapsible line with its own category picker
// and "Approve all", so twelve coffees from the same café are one decision. A group of one is drawn
// as a plain row. Membership is fixed when a row joins (by what the row showed then), so editing a
// row inside a group never moves it; approving uses each row's own picks. UI-thread only, like
// every bound view model here.
public sealed class StatementImportRowGroupViewModel : ViewModelBase
{
    private readonly List<StatementImportRowViewModel> rows = [];
    private readonly string currency;
    private bool isExpanded;
    private CategoryOptionViewModel? category;
    private string newCategoryName = string.Empty;

    // The category the user picked for the whole group (a parent, or one of its children), if they
    // did - rows that join later get it too.
    private Guid? chosenCategoryId;

    // Set while the group's own subcategory picker is being rebuilt from code, so that doesn't
    // count as the user choosing a child for the whole group.
    private bool isSyncingSubcategory;

    public StatementImportRowGroupViewModel(
        StatementImportRowGroupKey key,
        StatementImportRowViewModel firstRow,
        string currency,
        ObservableCollection<CategoryOptionViewModel> categoryOptions,
        CategoryTree categoryTree,
        Func<StatementImportRowGroupViewModel, Task> onApprove,
        Func<StatementImportRowGroupViewModel, Task> onCreateCategory)
    {
        Key = key;
        this.currency = currency;
        Name = firstRow.Description;
        Type = firstRow.Type;
        category = firstRow.Category;
        CategoryOptions = categoryOptions;
        Subcategory = new SubcategoryPickerViewModel(OnSubcategoryChanged);
        SyncSubcategory(() =>
        {
            Subcategory.Reset(categoryTree);
            if (firstRow.CategoryId is { } firstCategoryId)
            {
                Subcategory.SelectCategory(firstCategoryId);
            }
        });
        DisplayedRows = [];

        ToggleExpandedCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        ApproveCommand = new RelayCommand(() => _ = onApprove(this));
        CreateCategoryCommand = new RelayCommand(() => _ = onCreateCategory(this));

        Add(firstRow);
    }

    public StatementImportRowGroupKey Key { get; }

    public string Name { get; }

    public TransactionType Type { get; }

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

    // The chosen parent's children, when it has any: choosing one puts it on every row too.
    public SubcategoryPickerViewModel Subcategory { get; }

    // The group's own category picker: choosing a category puts it on every row in the group.
    // "+ New category" only takes effect once the name is committed (Enter / Add), like on a row.
    // A null is MAUI resetting the picker, never a choice - see StatementImportRowViewModel.Category.
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

            if (!SetProperty(ref category, value))
            {
                return;
            }

            RaiseCategoryChanged();
            SyncSubcategory(() => Subcategory.ShowChildrenOf(value is { IsCategory: true } ? value.Id : null));
            if (value is { IsCategory: true })
            {
                ApplyToRows(value.Id);
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

    public IReadOnlyList<StatementImportRowViewModel> Rows => rows;

    // What the group's row list is bound to: empty while a group of two or more is collapsed, so a
    // collapsed group costs one header line to draw rather than every row in it.
    public ObservableCollection<StatementImportRowViewModel> DisplayedRows { get; }

    public int Count => rows.Count;

    public bool IsMultiple => rows.Count > 1;

    public bool IsExpanded
    {
        get => isExpanded;
        private set
        {
            if (SetProperty(ref isExpanded, value))
            {
                SyncDisplayedRows();
            }
        }
    }

    public string CountText => Translator.GetPlural("StatementImport_GroupCount", rows.Count);

    public string TotalText => MoneyFormat.Format(rows.Sum(row => row.SignedAmount), currency);

    public string ApproveText => string.Format(Translator.Get("StatementImport_ApproveGroupFormat"), rows.Count);

    public ICommand ToggleExpandedCommand { get; }

    public ICommand ApproveCommand { get; }

    // Creates the category named in NewCategoryName and gives it to the whole group.
    public ICommand CreateCategoryCommand { get; }

    public void Add(StatementImportRowViewModel row)
    {
        var index = 0;
        while (index < rows.Count && rows[index].Date <= row.Date)
        {
            index++;
        }

        rows.Insert(index, row);
        if (chosenCategoryId is not null)
        {
            row.SetCategoryById(chosenCategoryId);
        }

        OnRowsChanged();
    }

    public void Remove(StatementImportRowViewModel row)
    {
        if (rows.Remove(row))
        {
            OnRowsChanged();
        }
    }

    // Puts the picker back without touching the rows - see StatementImportCategoriesViewModel.AddOption.
    public void RestoreCategory(CategoryOptionViewModel? value)
    {
        if (SetProperty(ref category, value))
        {
            RaiseCategoryChanged();
            SyncSubcategory(() => Subcategory.ShowChildrenOf(value is { IsCategory: true } ? value.Id : null));
        }
    }

    // A category was created (or found) for the name typed in the group's box.
    public void AdoptCategory(CategoryOptionViewModel option)
    {
        NewCategoryName = string.Empty;
        Category = option;
    }

    // The group's subcategory picker changed by the user: that child (or, for "No subcategory",
    // the parent itself) goes on every row.
    private void OnSubcategoryChanged()
    {
        if (!isSyncingSubcategory && Category is { IsCategory: true } parent)
        {
            ApplyToRows(parent.Id);
        }
    }

    private void ApplyToRows(Guid parentId)
    {
        chosenCategoryId = Subcategory.Resolve(parentId);
        foreach (var row in rows)
        {
            row.SetCategoryById(chosenCategoryId);
        }
    }

    private void SyncSubcategory(Action action)
    {
        isSyncingSubcategory = true;
        try
        {
            action();
        }
        finally
        {
            isSyncingSubcategory = false;
        }
    }

    private void OnRowsChanged()
    {
        SyncDisplayedRows();
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(IsMultiple));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(ApproveText));
    }

    private void SyncDisplayedRows()
    {
        CollectionSync.Apply(DisplayedRows, IsMultiple && !IsExpanded ? [] : rows);
    }

    private async Task ReassertCategoryAsync()
    {
        await Task.Yield();
        await RunOnMainThreadAsync(() => OnPropertyChanged(nameof(Category)));
    }

    private void RaiseCategoryChanged()
    {
        OnPropertyChanged(nameof(IsCreatingNewCategory));
        OnPropertyChanged(nameof(CategoryBorderColor));
    }
}

// Same sender name (ignoring case and surrounding spaces), category, type and other account.
public readonly record struct StatementImportRowGroupKey(string Name, Guid? CategoryId, TransactionType Type, Guid? OtherAccountId)
{
    public static StatementImportRowGroupKey For(StatementImportRowViewModel row) => new(
        row.Description.Trim().ToUpperInvariant(),
        row.CategoryId,
        row.Type,
        row.IsTransferType ? row.OtherAccount?.Id : null);
}
