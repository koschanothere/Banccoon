using System.Collections.ObjectModel;
using Banccoon.Core.Categories;

namespace Banccoon.App.ViewModels;

// The optional second step of every category picker. Category pickers only ever list parent
// (top-level) categories (CategoryOptionsHelper.Repopulate); once the chosen parent has children,
// this offers just that parent's own children, headed by "No subcategory". Leaving it there keeps
// the parent itself as the answer - parents are real, assignable categories.
//
// A call site composes one per picker: it calls ShowChildrenOf whenever its parent selection
// changes, and Resolve when it saves. Like every other UI-bound collection, Options must only be
// touched on the UI thread: call ShowChildrenOf/SelectCategory/UseTree from a picker's setter or
// inside RunOnMainThreadAsync, never straight after an await.
public sealed class SubcategoryPickerViewModel : ViewModelBase
{
    private readonly Action? onChanged;
    private readonly string headOptionKey;
    private CategoryTree tree = CategoryTree.Empty;
    private Guid? parentId;
    private CategoryOptionViewModel? selected;
    private bool isRebuilding;

    // headOptionKey names the first entry: "No subcategory" when choosing a category to file
    // something under, "All of the category" when the picker narrows a filter.
    public SubcategoryPickerViewModel(Action? onChanged = null, string headOptionKey = "Common_NoSubcategoryOption")
    {
        this.onChanged = onChanged;
        this.headOptionKey = headOptionKey;
        Options = [];
    }

    public ObservableCollection<CategoryOptionViewModel> Options { get; }

    public CategoryOptionViewModel? Selected
    {
        get => selected;
        set
        {
            // Clearing Options makes the bound Picker push null back in; Rebuild picks the real
            // selection itself afterwards.
            if (isRebuilding)
            {
                return;
            }

            // The list always starts with "No subcategory", so a null from the picker is never a
            // choice: it's MAUI resetting the picker while it builds the view (the same thing
            // StatementImportRowViewModel.Category guards against). Keep the choice and hand it
            // back once the picker is done.
            if (value is null && selected is not null && Options.Count > 0)
            {
                _ = ReassertSelectedAsync();
                return;
            }

            if (SetProperty(ref selected, value))
            {
                OnPropertyChanged(nameof(ChildId));
                onChanged?.Invoke();
            }
        }
    }

    // Shown only when the chosen parent has children.
    public bool IsVisible => Options.Count > 0;

    // The chosen child, or null when "No subcategory" (or nothing) is chosen.
    public Guid? ChildId => Selected is { IsCategory: true } option ? option.Id : null;

    public CategoryTree Tree => tree;

    // Swaps in a fresh category snapshot (after the call site reloads its categories) and
    // rebuilds for the current parent, keeping the chosen child if it's still one of its children.
    public void UseTree(CategoryTree categoryTree)
    {
        tree = categoryTree;
        Rebuild(parentId, ChildId);
    }

    // Starts over with a fresh snapshot and nothing chosen (e.g. when a form is reopened).
    public void Reset(CategoryTree categoryTree)
    {
        tree = categoryTree;
        Rebuild(null, null);
    }

    // Call whenever the first picker's choice changes: the id of the chosen parent, or null for
    // no choice / "+ New category". selectChildId preselects one of its children (e.g. when an
    // existing record is filed under a child). Choosing the same parent again keeps the chosen
    // child unless selectChildId says otherwise.
    public void ShowChildrenOf(Guid? chosenParentId, Guid? selectChildId = null)
    {
        if (chosenParentId == parentId && selectChildId is null)
        {
            return;
        }

        Rebuild(chosenParentId, selectChildId);
    }

    // Shows a stored category in the two pickers: returns the parent the first picker should
    // select, and selects the child here if the category is one.
    public Guid SelectCategory(Guid categoryId)
    {
        var (chosenParentId, childId) = tree.Split(categoryId);
        Rebuild(chosenParentId, childId);
        return chosenParentId;
    }

    // The category to save: the chosen child when the first picker still shows its parent,
    // otherwise whatever the first picker resolved to.
    public Guid? Resolve(Guid? chosenId) =>
        chosenId is { } id && id == parentId && ChildId is { } childId ? childId : chosenId;

    private async Task ReassertSelectedAsync()
    {
        await Task.Yield();
        await RunOnMainThreadAsync(() => OnPropertyChanged(nameof(Selected)));
    }

    private void Rebuild(Guid? chosenParentId, Guid? selectChildId)
    {
        parentId = chosenParentId;
        CategoryOptionViewModel? toSelect = null;
        isRebuilding = true;
        try
        {
            Options.Clear();
            var children = chosenParentId is { } id ? tree.ChildrenOf(id) : [];
            if (children.Count > 0)
            {
                toSelect = CategoryOptionViewModel.NoSubcategory(headOptionKey);
                Options.Add(toSelect);
                foreach (var child in children)
                {
                    var option = CategoryOptionViewModel.ForCategory(child);
                    Options.Add(option);
                    if (child.Id == selectChildId)
                    {
                        toSelect = option;
                    }
                }
            }
        }
        finally
        {
            isRebuilding = false;
        }

        OnPropertyChanged(nameof(IsVisible));
        Selected = toSelect;
    }
}
