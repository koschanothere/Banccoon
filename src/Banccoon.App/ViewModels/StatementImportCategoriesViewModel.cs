using Banccoon.App.Localization;
using Banccoon.Core.Categories;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

// Keeps the review's one shared category list (Review.CategoryOptions - every row's picker and the
// bulk bar's picker are bound to it) in step with the categories that exist. Composed into
// StatementImportReviewViewModel as Review.Categories.
// - A category named on a row is created as soon as the row asks for it (Enter in the name box, or
//   the Add button next to it) - or at the latest when the row is approved - and from then on it is
//   offered to every row, and every row that typed the same name is pointed at it.
// - A category Core creates behind the scenes (the "Other" fallback for an approval with no
//   category) is picked up after each approval (SyncAsync).
public sealed class StatementImportCategoriesViewModel : ViewModelBase
{
    private readonly StatementImportReviewViewModel review;
    private readonly ICategoryRepository categoryRepository;

    public StatementImportCategoriesViewModel(StatementImportReviewViewModel review, ICategoryRepository categoryRepository)
    {
        this.review = review;
        this.categoryRepository = categoryRepository;
    }

    // Resolves a picker selection to a category id. "Create new" with a name reuses an existing
    // category of that name (typically: another row typed the same new name and got there first)
    // or creates it; either way every row about to create that name is pointed at it.
    public async Task<Guid?> ResolveAsync(CategoryOptionViewModel? selected, string newCategoryName)
    {
        if (selected?.IsCreateNew != true)
        {
            return selected?.Id;
        }

        if (string.IsNullOrWhiteSpace(newCategoryName))
        {
            return null;
        }

        CategoryOptionViewModel? option = null;
        await RunOnMainThreadAsync(() => option = review.CategoryOptions.FirstOrDefault(candidate => !candidate.IsCreateNew && NamesMatch(candidate.Name, newCategoryName)));
        if (option is null)
        {
            var (_, newOption) = await CategoryOptionsHelper.ResolveOrCreateAsync(selected, newCategoryName, categoryRepository);
            option = newOption!;
            await RunOnMainThreadAsync(() => AddOption(option));
        }

        var resolved = option;
        await RunOnMainThreadAsync(() =>
        {
            foreach (var row in review.Rows.Where(row => row.IsCreatingNewCategory && NamesMatch(row.NewCategoryName, resolved.Name)))
            {
                row.AdoptCategory(resolved);
            }
        });

        return resolved.Id;
    }

    // A row's Add button, or Enter in its new-category name box.
    public Task CreateForRowAsync(StatementImportRowViewModel row)
    {
        return review.RunExclusiveAsync(async () =>
        {
            CategoryOptionViewModel? selected = null;
            var name = string.Empty;
            await RunOnMainThreadAsync(() =>
            {
                selected = row.Category;
                name = row.NewCategoryName;
            });

            if (selected?.IsCreateNew != true)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                await review.SetStatusAsync(Translator.Get("StatementImport_NameNewCategoryFirst"));
                return;
            }

            await ResolveAsync(selected, name);
        });
    }

    // A group's Add button, or Enter in its new-category name box: the new category goes on every
    // row of the group.
    public Task CreateForGroupAsync(StatementImportRowGroupViewModel group)
    {
        return review.RunExclusiveAsync(async () =>
        {
            CategoryOptionViewModel? selected = null;
            var name = string.Empty;
            await RunOnMainThreadAsync(() =>
            {
                selected = group.Category;
                name = group.NewCategoryName;
            });

            if (selected?.IsCreateNew != true)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                await review.SetStatusAsync(Translator.Get("StatementImport_NameNewCategoryFirst"));
                return;
            }

            var categoryId = await ResolveAsync(selected, name);
            await RunOnMainThreadAsync(() =>
            {
                if (review.CategoryOptions.FirstOrDefault(option => !option.IsCreateNew && option.Id == categoryId) is { } option)
                {
                    group.AdoptCategory(option);
                }
            });
        });
    }

    // Offers every parent category that exists but isn't in the list yet (children are only ever
    // offered through a subcategory picker).
    public async Task SyncAsync()
    {
        var categories = await categoryRepository.GetAllAsync();
        await RunOnMainThreadAsync(() =>
        {
            foreach (var category in new CategoryTree(categories).TopLevel.Where(category => !review.CategoryOptions.Any(option => !option.IsCreateNew && option.Id == category.Id)))
            {
                AddOption(CategoryOptionViewModel.ForCategory(category));
            }
        });
    }

    // UI-thread only. Inserts in name order, then puts every row's (and the bulk bar's and each
    // ready group's) selection back. MAUI's Picker handles an insert at or before its selected item by re-reading the item
    // at its OLD index (Picker.AddItems calls UpdateSelectedItem(SelectedIndex)) and writes that back
    // through the two-way SelectedItem binding - so without this, a row showing "+ New category"
    // silently became the category just created and lost its name box, and any row whose category
    // sorted after the new one shifted to its neighbour. Recently reviewed rows are included: they
    // can come back through Undo, and their old pickers may not have let go of the list yet.
    public void AddOption(CategoryOptionViewModel option)
    {
        var rowSelections = review.Rows
            .Concat(review.RecentlyReviewedRows)
            .Select(row => (Row: row, Category: row.Category))
            .ToList();
        var bulkSelection = review.Bulk.BulkCategory;
        var groupSelections = review.Sections.ReadyGroups
            .Select(group => (Group: group, Category: group.Category))
            .ToList();

        CategoryOptionsHelper.InsertSorted(review.CategoryOptions, option);

        // A group picker knocked off its category by the insert also pushed that wrong category onto
        // its rows - putting the rows back after the insert has run its course undoes that too.
        foreach (var (row, category) in rowSelections)
        {
            row.RestoreCategory(category);
        }

        foreach (var (group, category) in groupSelections)
        {
            group.RestoreCategory(category);
        }

        review.Bulk.BulkCategory = bulkSelection;
    }

    private static bool NamesMatch(string left, string right)
    {
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
