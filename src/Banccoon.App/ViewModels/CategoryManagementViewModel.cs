using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Appearance;
using Banccoon.Core.Categories;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

// Renders categories as a "sea of boxes" (each box's background is its own color). Tapping a box
// opens a single shared color-picker popup (ActiveColorPickerBox/IsColorPickerOpen, rendered by the
// page as an overlay rather than inline, so the popup isn't constrained to the box's own compact
// size); tapping the SAME box again while its own picker is open switches to renaming it instead of
// needing a dedicated rename button. Dragging one box onto another merges the dragged category into
// the drop target. Select mode replaces a per-box delete button with multi-select + one "Delete
// selected" action, avoiding a delete affordance on every single box.
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
            Boxes.Clear();
            foreach (var category in ordered)
            {
                Boxes.Add(new CategoryBoxViewModel(
                    category,
                    HandleBoxTapped,
                    StartDrag,
                    box => _ = HandleDropAsync(box),
                    (box, newName) => RenameAsync(box.Id, newName),
                    SetColorAsync)
                {
                    IsSelectModeActive = IsSelectMode
                });
            }

            StatusText = string.Empty;
        });
    }

    private void HandleBoxTapped(CategoryBoxViewModel box)
    {
        if (IsSelectMode)
        {
            box.IsSelected = !box.IsSelected;
            return;
        }

        foreach (var other in Boxes)
        {
            if (other != box)
            {
                other.IsRenaming = false;
            }
        }

        if (ActiveColorPickerBox == box)
        {
            ActiveColorPickerBox = null;
            box.StartRenameCommand.Execute(null);
            return;
        }

        ActiveColorPickerBox = box;
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

    private async Task RenameAsync(Guid id, string newName)
    {
        var category = await categoryRepository.GetByIdAsync(id);
        if (category is null)
        {
            return;
        }

        await categoryRepository.SaveAsync(category with { Name = newName });
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
            StatusText = "Enter a category name.";
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
