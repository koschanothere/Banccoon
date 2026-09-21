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

    public CategoryManagementViewModel(
        ICategoryRepository categoryRepository,
        ICategoryManagementService categoryManagementService,
        Func<Task> onChanged)
    {
        this.categoryRepository = categoryRepository;
        this.categoryManagementService = categoryManagementService;
        this.onChanged = onChanged;

        Boxes = [];

        CloseColorPickerCommand = new RelayCommand(() => ActiveColorPickerBox = null);
        ToggleSelectModeCommand = new RelayCommand(ToggleSelectMode);
        DeleteSelectedCommand = new RelayCommand(() => _ = DeleteSelectedAsync());
        StartAddCategoryCommand = new RelayCommand(() =>
        {
            NewCategoryName = string.Empty;
            StatusText = string.Empty;
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
            }
        }
    }

    public bool IsColorPickerOpen => ActiveColorPickerBox is not null;

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
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var ordered = categories.OrderBy(category => category.Name).ToList();

        // Mutates a collection bound to live UI - must run on the UI thread, which the await
        // above may have resumed off of (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            ActiveColorPickerBox = null;
            ReconcileBoxes(ordered);
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
    // are additions, removals, and a color change - never a reorder of two otherwise-unchanged
    // categories.
    private void ReconcileBoxes(IReadOnlyList<Category> ordered)
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
                existingBox.UpdateColor(category.Color);
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
                    IsSelectModeActive = IsSelectMode
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

        var category = new Category(Guid.NewGuid(), NewCategoryName.Trim());
        await categoryRepository.SaveAsync(category);

        await RunOnMainThreadAsync(() =>
        {
            IsAddingCategory = false;
            NewCategoryName = string.Empty;
        });

        await RefreshAsync();
        await onChanged();
    }
}
