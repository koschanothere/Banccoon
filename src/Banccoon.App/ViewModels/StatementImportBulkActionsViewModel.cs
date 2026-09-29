using System.ComponentModel;
using System.Windows.Input;
using Banccoon.App.Localization;

namespace Banccoon.App.ViewModels;

// Select mode for the statement import review step (composed into StatementImportReviewViewModel
// as Review.Bulk): which rows are selected, the bulk category picker, and the bulk actions -
// apply a category to the selection, approve it, or skip it. The actual approving/skipping, and
// the one-at-a-time gate every review action goes through, stay on the parent.
public sealed class StatementImportBulkActionsViewModel : ViewModelBase
{
    private readonly StatementImportReviewViewModel review;

    private bool selectMode;
    private CategoryOptionViewModel? bulkCategory;
    private string newBulkCategoryName = string.Empty;
    private string selectionSummaryText = string.Empty;

    public StatementImportBulkActionsViewModel(StatementImportReviewViewModel review)
    {
        this.review = review;
        Subcategory = new SubcategoryPickerViewModel();

        ToggleSelectModeCommand = new RelayCommand(ToggleSelectMode);
        ToggleSelectAllCommand = new RelayCommand(ToggleSelectAll);
        SelectDuplicatesCommand = new RelayCommand(SelectDuplicates);
        ApplyCategoryCommand = new RelayCommand(() => _ = ApplyCategoryAsync());
        ApproveSelectedCommand = new RelayCommand(() => _ = ApproveSelectedAsync());
        SkipSelectedCommand = new RelayCommand(() => _ = SkipSelectedAsync());
    }

    public bool SelectMode
    {
        get => selectMode;
        private set => SetProperty(ref selectMode, value);
    }

    public bool AreAllRowsSelected => review.Rows.Count > 0 && review.Rows.All(row => row.IsSelected);

    public bool CanSelectAll => !AreAllRowsSelected;

    public bool HasDuplicates => review.Rows.Any(row => row.IsDuplicate);

    public CategoryOptionViewModel? BulkCategory
    {
        get => bulkCategory;
        set
        {
            if (SetProperty(ref bulkCategory, value))
            {
                OnPropertyChanged(nameof(IsCreatingNewBulkCategory));
                Subcategory.ShowChildrenOf(value is { IsCategory: true } ? value.Id : null);
            }
        }
    }

    // The bulk category's children, when it has any (see SubcategoryPickerViewModel).
    public SubcategoryPickerViewModel Subcategory { get; }

    public bool IsCreatingNewBulkCategory => BulkCategory?.IsCreateNew == true;

    public string NewBulkCategoryName
    {
        get => newBulkCategoryName;
        set => SetProperty(ref newBulkCategoryName, value);
    }

    public string SelectionSummaryText
    {
        get => selectionSummaryText;
        private set => SetProperty(ref selectionSummaryText, value);
    }

    public ICommand ToggleSelectModeCommand { get; }

    public ICommand ToggleSelectAllCommand { get; }

    public ICommand SelectDuplicatesCommand { get; }

    public ICommand ApplyCategoryCommand { get; }

    public ICommand ApproveSelectedCommand { get; }

    public ICommand SkipSelectedCommand { get; }

    // UI-thread only - called by the parent from inside its own RunOnMainThreadAsync blocks.
    public void OnRowAdded(StatementImportRowViewModel row)
    {
        row.IsSelectModeActive = SelectMode;
        row.PropertyChanged += OnRowPropertyChanged;
    }

    public void OnRowRemoved(StatementImportRowViewModel row)
    {
        row.PropertyChanged -= OnRowPropertyChanged;
        if (review.Rows.Count == 0)
        {
            SelectMode = false;
        }
    }

    public void OnRowsChanged()
    {
        OnPropertyChanged(nameof(HasDuplicates));
        UpdateSelectionSummary();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StatementImportRowViewModel.IsSelected))
        {
            UpdateSelectionSummary();
        }
    }

    private void ToggleSelectMode()
    {
        SelectMode = !SelectMode;
        ApplySelectModeToRows();
    }

    private void ExitSelectMode()
    {
        SelectMode = false;
        BulkCategory = null;
        NewBulkCategoryName = string.Empty;
        ApplySelectModeToRows();
    }

    private void ApplySelectModeToRows()
    {
        foreach (var row in review.Rows)
        {
            row.IsSelectModeActive = SelectMode;
            if (!SelectMode)
            {
                row.IsSelected = false;
            }
        }

        UpdateSelectionSummary();
    }

    private void ToggleSelectAll()
    {
        var select = !AreAllRowsSelected;
        foreach (var row in review.Rows)
        {
            row.IsSelected = select;
        }

        UpdateSelectionSummary();
    }

    // Re-importing an overlapping statement period flags every already-recorded row as a possible
    // duplicate; selecting exactly those makes "Skip selected" a two-click cleanup instead of one
    // Skip per row. Replaces the current selection rather than adding to it.
    private void SelectDuplicates()
    {
        foreach (var row in review.Rows)
        {
            row.IsSelected = row.IsDuplicate;
        }

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var count = review.Rows.Count(row => row.IsSelected);
        SelectionSummaryText = Translator.GetPlural("Common_SelectionCount", count);
        OnPropertyChanged(nameof(AreAllRowsSelected));
        OnPropertyChanged(nameof(CanSelectAll));
    }

    // Sets the bulk category on every selected row's own picker without approving anything, so the
    // rows can still be checked (or individually adjusted) before approval. The bulk picker resets
    // afterwards, so "Approve selected" then approves each row with what its own picker now shows.
    private async Task ApplyCategoryAsync()
    {
        if (BulkCategory is null || !await CheckBulkCategoryNamedAsync())
        {
            return;
        }

        await review.RunExclusiveAsync(async () =>
        {
            var categoryId = Subcategory.Resolve(await review.Categories.ResolveAsync(BulkCategory, NewBulkCategoryName));
            await RunOnMainThreadAsync(() =>
            {
                if (categoryId is not null)
                {
                    foreach (var row in review.Rows.Where(row => row.IsSelected))
                    {
                        row.SetCategoryById(categoryId);
                        row.NewCategoryName = string.Empty;
                    }
                }

                BulkCategory = null;
                NewBulkCategoryName = string.Empty;
            });
        });
    }

    private async Task ApproveSelectedAsync()
    {
        if (!await CheckBulkCategoryNamedAsync())
        {
            return;
        }

        var selected = await BeginSelectedRowActionsAsync();
        if (selected.Count == 0)
        {
            return;
        }

        try
        {
            await review.RunExclusiveAsync(async () =>
            {
                // Validated up front, before anything is approved, so a problem on one row doesn't
                // leave the selection half-approved. A bulk category overrides every row's own
                // category, so the rows' own "create new" names only matter without one.
                if (await review.GetApprovalProblemAsync(selected, checkRowCategories: BulkCategory is null) is { } problem)
                {
                    await review.SetStatusAsync(problem);
                    return;
                }

                // A bulk "create new" is resolved once, up front, so every selected row lands in the
                // same new category rather than creating one per row.
                var bulkCategoryId = Subcategory.Resolve(await review.Categories.ResolveAsync(BulkCategory, NewBulkCategoryName));
                foreach (var row in selected)
                {
                    await review.ApproveAsync(row, bulkCategoryId);
                }

                await RunOnMainThreadAsync(ExitSelectMode);
            });
        }
        finally
        {
            await EndRowActionsAsync(selected);
        }
    }

    private async Task SkipSelectedAsync()
    {
        var selected = await BeginSelectedRowActionsAsync();
        if (selected.Count == 0)
        {
            return;
        }

        try
        {
            await review.RunExclusiveAsync(async () =>
            {
                foreach (var row in selected)
                {
                    await review.SkipAsync(row);
                }

                await RunOnMainThreadAsync(ExitSelectMode);
            });
        }
        finally
        {
            await EndRowActionsAsync(selected);
        }
    }

    private async Task<bool> CheckBulkCategoryNamedAsync()
    {
        if (IsCreatingNewBulkCategory && string.IsNullOrWhiteSpace(NewBulkCategoryName))
        {
            await review.SetStatusAsync(Translator.Get("StatementImport_NameNewCategoryFirst"));
            return false;
        }

        return true;
    }

    // Marks every selected row busy up front (skipping any that already have their own approve or
    // skip queued), so their per-row buttons can't start a second action on them meanwhile.
    private async Task<IReadOnlyList<StatementImportRowViewModel>> BeginSelectedRowActionsAsync()
    {
        IReadOnlyList<StatementImportRowViewModel> selected = [];
        await RunOnMainThreadAsync(() => selected = review.Rows.Where(row => row.IsSelected && row.TryBeginAction()).ToList());
        return selected;
    }

    private static Task EndRowActionsAsync(IReadOnlyList<StatementImportRowViewModel> rows)
    {
        return RunOnMainThreadAsync(() =>
        {
            foreach (var row in rows)
            {
                row.EndAction();
            }
        });
    }
}
