using System.Collections.ObjectModel;
using Banccoon.Core.Appearance;
using Banccoon.Core.Categories;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

// Renders categories as a "sea of boxes" (each box's background is its own color) rather than a
// list + separate merge-picker: tap opens the color picker inline, double-tap renames, and
// dragging one box onto another merges the dragged category into the drop target.
public sealed class CategoryManagementViewModel : ViewModelBase
{
    private readonly ICategoryRepository categoryRepository;
    private readonly ICategoryManagementService categoryManagementService;
    private readonly Func<Task> onChanged;

    private CategoryBoxViewModel? draggedCategory;
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
    }

    public ObservableCollection<CategoryBoxViewModel> Boxes { get; }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

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
            Boxes.Clear();
            foreach (var category in ordered)
            {
                Boxes.Add(new CategoryBoxViewModel(
                    category,
                    StartDrag,
                    box => _ = HandleDropAsync(box),
                    (box, newName) => RenameAsync(box.Id, newName),
                    SetColorAsync,
                    DeleteAsync));
            }

            StatusText = string.Empty;
        });
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

    private async Task DeleteAsync(Guid id)
    {
        await categoryRepository.DeleteAsync(id);
        await RefreshAsync();
        await onChanged();
    }
}
