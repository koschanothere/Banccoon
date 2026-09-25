using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
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

    // The category the user picked for the whole group, if they did - rows that join later get it too.
    private CategoryOptionViewModel? chosenCategory;

    public StatementImportRowGroupViewModel(
        StatementImportRowGroupKey key,
        StatementImportRowViewModel firstRow,
        string currency,
        ObservableCollection<CategoryOptionViewModel> categoryOptions,
        Func<StatementImportRowGroupViewModel, Task> onApprove,
        Func<StatementImportRowGroupViewModel, Task> onCreateCategory)
    {
        Key = key;
        this.currency = currency;
        Name = firstRow.Description;
        Type = firstRow.Type;
        category = firstRow.Category;
        CategoryOptions = categoryOptions;
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

    // The group's own category picker: choosing a category puts it on every row in the group.
    // "+ New category" only takes effect once the name is committed (Enter / Add), like on a row.
    public CategoryOptionViewModel? Category
    {
        get => category;
        set
        {
            if (!SetProperty(ref category, value))
            {
                return;
            }

            RaiseCategoryChanged();
            if (value is { IsCreateNew: false })
            {
                chosenCategory = value;
                foreach (var row in rows)
                {
                    row.Category = value;
                }
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
        if (chosenCategory is not null)
        {
            row.Category = chosenCategory;
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
        }
    }

    // A category was created (or found) for the name typed in the group's box.
    public void AdoptCategory(CategoryOptionViewModel option)
    {
        NewCategoryName = string.Empty;
        Category = option;
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
        row.Category?.Id,
        row.Type,
        row.IsTransferType ? row.OtherAccount?.Id : null);
}
