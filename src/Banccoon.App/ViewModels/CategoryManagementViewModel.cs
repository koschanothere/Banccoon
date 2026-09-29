using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Localization;
using Banccoon.Core.Appearance;
using Banccoon.Core.Categories;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

// Renders categories as a "sea of boxes" (each box's background is its own color). Tapping a box
// opens/closes a single shared color-picker popup (ActiveColorPickerBox/IsColorPickerOpen, rendered
// by the page as an overlay rather than inline, so the popup isn't constrained to the box's own
// compact size). Dragging one box onto another merges the dragged category into the drop target.
// Select mode replaces a per-box delete button with multi-select + one "Delete selected" action,
// avoiding a delete affordance on every single box. Renaming was tried (tap-while-picker-open) and
// removed - it didn't work in practice and wasn't worth further iteration.
//
// Categories are two levels deep. Each parent's children sit right after it, drawn smaller with a
// "↳" marker (an ordering change plus a small cue, not a nested layout). A child's color is its
// parent's, so tapping a child opens its own popup without swatches: it says whose color it
// follows and, like every popup, offers "Parent category" - which is how an existing category is
// put under a parent, moved, or made top-level again. "+ Add category" can pick a parent too.
public sealed class CategoryManagementViewModel : ViewModelBase
{
    private readonly ICategoryRepository categoryRepository;
    private readonly ICategoryManagementService categoryManagementService;
    private readonly Func<Task> onChanged;

    private CategoryBoxViewModel? draggedCategory;
    private CategoryBoxViewModel? activeColorPickerBox;
    private bool isSelectMode;
    private bool isAddingCategory;
    private string newCategoryName = string.Empty;
    private string statusText = string.Empty;
    private NamedOptionViewModel? newCategoryParent;
    private NamedOptionViewModel? activeBoxParent;
    private IReadOnlyList<Category> categories = [];

    // Set while ActiveBoxParentOptions is rebuilt, so the picker resetting itself isn't saved as a
    // move (the same pattern as TransactionsViewModel.isRebuildingOptions).
    private bool isRebuildingParentOptions;

    public CategoryManagementViewModel(
        ICategoryRepository categoryRepository,
        ICategoryManagementService categoryManagementService,
        Func<Task> onChanged)
    {
        this.categoryRepository = categoryRepository;
        this.categoryManagementService = categoryManagementService;
        this.onChanged = onChanged;

        Boxes = [];
        NewCategoryParentOptions = [];
        ActiveBoxParentOptions = [];

        CloseColorPickerCommand = new RelayCommand(() => ActiveColorPickerBox = null);
        ToggleSelectModeCommand = new RelayCommand(ToggleSelectMode);
        DeleteSelectedCommand = new RelayCommand(() => _ = DeleteSelectedAsync());
        StartAddCategoryCommand = new RelayCommand(() =>
        {
            NewCategoryName = string.Empty;
            StatusText = string.Empty;
            RebuildParentOptions(NewCategoryParentOptions, excludeId: null);
            NewCategoryParent = NewCategoryParentOptions.FirstOrDefault();
            IsAddingCategory = true;
        });
        CancelAddCategoryCommand = new RelayCommand(() => IsAddingCategory = false);
        ConfirmAddCategoryCommand = new RelayCommand(() => _ = ConfirmAddCategoryAsync());
    }

    public ObservableCollection<CategoryBoxViewModel> Boxes { get; }

    public CategoryBoxViewModel? ActiveColorPickerBox
    {
        get => activeColorPickerBox;
        private set
        {
            if (SetProperty(ref activeColorPickerBox, value))
            {
                OnPropertyChanged(nameof(IsColorPickerOpen));
                OnPropertyChanged(nameof(ActiveColorPickerBoxColorForLabel));
                OnPropertyChanged(nameof(CanChooseActiveColor));
                OnPropertyChanged(nameof(ActiveBoxColorFollowsText));
                OnPropertyChanged(nameof(CanChangeActiveParent));
                RebuildActiveBoxParentOptions();
            }
        }
    }

    public bool IsColorPickerOpen => ActiveColorPickerBox is not null;

    public string ActiveColorPickerBoxColorForLabel => ActiveColorPickerBox is { } box
        ? box.IsChild ? box.Name : string.Format(Translator.Get("Settings_ColorForFormat"), box.Name)
        : string.Empty;

    // Only a parent's color can be chosen; a child's is always its parent's.
    public bool CanChooseActiveColor => ActiveColorPickerBox is { IsChild: false };

    // "Its color follows Food." in a child's popup, instead of the swatches.
    public string ActiveBoxColorFollowsText => ActiveColorPickerBox is { IsChild: true, ParentCategoryId: { } parentId }
        && categories.FirstOrDefault(category => category.Id == parentId) is { } parent
            ? string.Format(Translator.Get("Settings_ColorFollowsParentFormat"), parent.Name)
            : string.Empty;

    // A category with children stays top-level (two levels only), so it gets a note instead.
    public bool CanChangeActiveParent => ActiveColorPickerBox is { HasChildren: false };

    // "None (top level)" plus every other parent: where the tapped category sits. Choosing one
    // saves at once, like choosing a color.
    public ObservableCollection<NamedOptionViewModel> ActiveBoxParentOptions { get; }

    public NamedOptionViewModel? ActiveBoxParent
    {
        get => activeBoxParent;
        set
        {
            if (SetProperty(ref activeBoxParent, value) && !isRebuildingParentOptions && value is not null && ActiveColorPickerBox is { } box)
            {
                var parentId = value.Id == Guid.Empty ? (Guid?)null : value.Id;
                if (parentId != box.ParentCategoryId)
                {
                    _ = SetParentAsync(box.Id, parentId);
                }
            }
        }
    }

    // "+ Add category"'s optional parent; defaults to none (a new parent category).
    public ObservableCollection<NamedOptionViewModel> NewCategoryParentOptions { get; }

    public NamedOptionViewModel? NewCategoryParent
    {
        get => newCategoryParent;
        set => SetProperty(ref newCategoryParent, value);
    }

    public bool IsSelectMode
    {
        get => isSelectMode;
        private set => SetProperty(ref isSelectMode, value);
    }

    public bool IsAddingCategory
    {
        get => isAddingCategory;
        private set => SetProperty(ref isAddingCategory, value);
    }

    public string NewCategoryName
    {
        get => newCategoryName;
        set => SetProperty(ref newCategoryName, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ICommand CloseColorPickerCommand { get; }

    public ICommand ToggleSelectModeCommand { get; }

    public ICommand DeleteSelectedCommand { get; }

    public ICommand StartAddCategoryCommand { get; }

    public ICommand CancelAddCategoryCommand { get; }

    public ICommand ConfirmAddCategoryCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await categoryRepository.GetAllAsync(cancellationToken);

        // Parents by name, each followed by its own children by name.
        var tree = new CategoryTree(loaded);
        var ordered = tree.TopLevel
            .SelectMany(parent => new[] { parent }.Concat(tree.ChildrenOf(parent.Id)))
            .ToList();

        // Mutates a collection bound to live UI - must run on the UI thread, which the await
        // above may have resumed off of (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            categories = loaded;
            ActiveColorPickerBox = null;
            ReconcileBoxes(ordered, tree);
            StatusText = string.Empty;
        });
    }

    // Updates Boxes to match `ordered` by reusing/updating existing CategoryBoxViewModel instances
    // wherever the category still exists, instead of the previous Clear()+rebuild-everything
    // approach - see CategoryBoxViewModel.UpdateColor for why that mattered (BindableLayout isn't
    // virtualized, so every Add() was a real native view built from scratch). RefreshAsync runs on
    // every single Settings visit via InitializeAsync, not just after an actual edit, so most calls
    // had nothing to change at all. Category names are immutable once created (renaming was tried
    // and removed - see the class remarks above), so the only things that can differ between calls
    // are additions, removals, a color change, and a change of parent (which moves a box next to
    // its new parent - handled by the Move below).
    private void ReconcileBoxes(IReadOnlyList<Category> ordered, CategoryTree tree)
    {
        var existingById = Boxes.ToDictionary(box => box.Id);
        var orderedIds = new HashSet<Guid>(ordered.Select(category => category.Id));

        for (var index = Boxes.Count - 1; index >= 0; index--)
        {
            if (!orderedIds.Contains(Boxes[index].Id))
            {
                Boxes.RemoveAt(index);
            }
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            var category = ordered[index];
            if (existingById.TryGetValue(category.Id, out var existingBox))
            {
                existingBox.UpdateFrom(category);
                existingBox.HasChildren = tree.HasChildren(category.Id);
                existingBox.IsSelectModeActive = IsSelectMode;

                var currentIndex = Boxes.IndexOf(existingBox);
                if (currentIndex != index)
                {
                    Boxes.Move(currentIndex, index);
                }
            }
            else
            {
                Boxes.Insert(index, new CategoryBoxViewModel(
                    category,
                    HandleBoxTapped,
                    StartDrag,
                    box => _ = HandleDropAsync(box),
                    SetColorAsync)
                {
                    IsSelectModeActive = IsSelectMode,
                    HasChildren = tree.HasChildren(category.Id)
                });
            }
        }
    }

    private void HandleBoxTapped(CategoryBoxViewModel box)
    {
        if (IsSelectMode)
        {
            box.IsSelected = !box.IsSelected;
            return;
        }

        ActiveColorPickerBox = ActiveColorPickerBox == box ? null : box;
    }

    private void ToggleSelectMode()
    {
        IsSelectMode = !IsSelectMode;
        ActiveColorPickerBox = null;

        foreach (var box in Boxes)
        {
            box.IsSelectModeActive = IsSelectMode;
            if (!IsSelectMode)
            {
                box.IsSelected = false;
            }
        }
    }

    private void StartDrag(CategoryBoxViewModel box)
    {
        draggedCategory = box;
    }

    private async Task HandleDropAsync(CategoryBoxViewModel target)
    {
        var source = draggedCategory;
        draggedCategory = null;

        if (source is null || source.Id == target.Id)
        {
            return;
        }

        await categoryManagementService.MergeAsync(source.Id, target.Id);
        await RefreshAsync();
        await onChanged();
    }

    private async Task SetColorAsync(Guid id, CategoryColor color)
    {
        var category = await categoryRepository.GetByIdAsync(id);
        if (category is null)
        {
            return;
        }

        await categoryRepository.SaveAsync(category with { Color = color });
        await RefreshAsync();
        await onChanged();
    }

    private async Task DeleteSelectedAsync()
    {
        var idsToDelete = Boxes.Where(box => box.IsSelected).Select(box => box.Id).ToList();
        IsSelectMode = false;

        if (idsToDelete.Count == 0)
        {
            return;
        }

        foreach (var id in idsToDelete)
        {
            await categoryRepository.DeleteAsync(id);
        }

        await RefreshAsync();
        await onChanged();
    }

    private async Task ConfirmAddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            StatusText = Translator.Get("Settings_EnterCategoryName");
            return;
        }

        // Core copies the parent's color onto a child (HierarchicalCategoryRepository).
        var parentId = NewCategoryParent is { } parent && parent.Id != Guid.Empty ? parent.Id : (Guid?)null;
        var category = new Category(Guid.NewGuid(), NewCategoryName.Trim(), ParentCategoryId: parentId);
        await categoryRepository.SaveAsync(category);

        await RunOnMainThreadAsync(() =>
        {
            IsAddingCategory = false;
            NewCategoryName = string.Empty;
        });

        await RefreshAsync();
        await onChanged();
    }

    private async Task SetParentAsync(Guid id, Guid? parentId)
    {
        var category = await categoryRepository.GetByIdAsync(id);
        if (category is null)
        {
            return;
        }

        await categoryRepository.SaveAsync(category with { ParentCategoryId = parentId });
        await RefreshAsync();
        await onChanged();
    }

    // UI-thread only.
    private void RebuildActiveBoxParentOptions()
    {
        isRebuildingParentOptions = true;
        try
        {
            var box = ActiveColorPickerBox;
            RebuildParentOptions(ActiveBoxParentOptions, box?.Id);
            ActiveBoxParent = box?.ParentCategoryId is { } parentId
                ? ActiveBoxParentOptions.FirstOrDefault(option => option.Id == parentId)
                : ActiveBoxParentOptions.FirstOrDefault();
        }
        finally
        {
            isRebuildingParentOptions = false;
        }
    }

    // "None (top level)" (Guid.Empty) plus every parent category, except excludeId itself.
    private void RebuildParentOptions(ObservableCollection<NamedOptionViewModel> options, Guid? excludeId)
    {
        options.Clear();
        options.Add(new NamedOptionViewModel(Guid.Empty, Translator.Get("Settings_NoParentOption")));
        foreach (var parent in new CategoryTree(categories).TopLevel.Where(parent => parent.Id != excludeId))
        {
            options.Add(new NamedOptionViewModel(parent.Id, parent.Name));
        }
    }
}
