using System.Collections.ObjectModel;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

// Shared behavior for every category picker that supports inline "create new category" (Statement
// Import review rows, the Add Transaction form, the Schedule form): build the option list with the
// create-new sentinel always last, and resolve a selection into a real category id - creating one
// first if the sentinel was picked.
//
// ResolveOrCreateAsync deliberately does NOT touch the options collection itself: it awaits a
// repository call, and Microsoft.Data.Sqlite's async methods use ConfigureAwait(false) internally,
// so the continuation can resume off the UI thread. Mutating an ObservableCollection bound to a
// live Picker from there would crash (see ViewModelBase.RunOnMainThreadAsync) - so callers get the
// new option back as data and insert it themselves via InsertBeforeSentinel, wrapped in their own
// RunOnMainThreadAsync the same way every other post-await UI mutation in this app is.
public static class CategoryOptionsHelper
{
    public static void Repopulate(ObservableCollection<CategoryOptionViewModel> options, IEnumerable<Category> categories)
    {
        options.Clear();
        foreach (var category in categories.OrderBy(category => category.Name))
        {
            options.Add(CategoryOptionViewModel.ForCategory(category));
        }

        options.Add(CategoryOptionViewModel.CreateNewSentinel());
    }

    public static void InsertBeforeSentinel(ObservableCollection<CategoryOptionViewModel> options, CategoryOptionViewModel option)
    {
        var insertIndex = options.Count > 0 && options[^1].IsCreateNew ? options.Count - 1 : options.Count;
        options.Insert(insertIndex, option);
    }

    // Like InsertBeforeSentinel, but keeps the name order Repopulate gave the list.
    public static void InsertSorted(ObservableCollection<CategoryOptionViewModel> options, CategoryOptionViewModel option)
    {
        var index = 0;
        while (index < options.Count
            && !options[index].IsCreateNew
            && string.Compare(options[index].Name, option.Name, StringComparison.CurrentCulture) <= 0)
        {
            index++;
        }

        options.Insert(index, option);
    }

    // Returns (null, null) when nothing usable was selected (nothing picked, or "create new"
    // picked with no name typed yet). NewOption is non-null only when a category was actually
    // just created - the caller is responsible for adding it to whichever options list(s) should
    // reflect it, via InsertBeforeSentinel, on the UI thread.
    public static async Task<(Guid? CategoryId, CategoryOptionViewModel? NewOption)> ResolveOrCreateAsync(
        CategoryOptionViewModel? selected,
        string newCategoryName,
        ICategoryRepository categoryRepository)
    {
        if (selected is null)
        {
            return (null, null);
        }

        if (!selected.IsCreateNew)
        {
            return (selected.Id, null);
        }

        if (string.IsNullOrWhiteSpace(newCategoryName))
        {
            return (null, null);
        }

        var category = new Category(Guid.NewGuid(), newCategoryName.Trim());
        await categoryRepository.SaveAsync(category);

        return (category.Id, CategoryOptionViewModel.ForCategory(category));
    }
}
