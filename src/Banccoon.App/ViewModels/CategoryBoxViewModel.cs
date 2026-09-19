using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Appearance;
using Banccoon.Core.Models;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

public sealed class CategoryBoxViewModel : ViewModelBase
{
    private bool isRenaming;
    private bool isSelected;
    private bool isSelectModeActive;
    private string editName;

    public CategoryBoxViewModel(
        Category category,
        Action<CategoryBoxViewModel> onTap,
        Action<CategoryBoxViewModel> onStartDrag,
        Action<CategoryBoxViewModel> onDrop,
        Func<CategoryBoxViewModel, string, Task> onRename,
        Func<Guid, CategoryColor, Task> onSetColor)
    {
        Id = category.Id;
        Name = category.Name;
        Color = CategoryColorPalette.GetColorForCategory(category.Id, category.Color);
        editName = Name;

        ColorSwatches = CategoryColorPalette.AllColors
            .Select(color => new CategoryColorSwatchViewModel(color, category.Color == color, selectedColor => _ = onSetColor(Id, selectedColor)))
            .ToList();

        TapCommand = new RelayCommand(() => onTap(this));
        StartDragCommand = new RelayCommand(() => onStartDrag(this));
        DropCommand = new RelayCommand(() => onDrop(this));
        StartRenameCommand = new RelayCommand(() => IsRenaming = true);
        CommitRenameCommand = new RelayCommand(() => _ = CommitRenameAsync(onRename));
    }

    public Guid Id { get; }

    public string Name { get; }

    public Color Color { get; }

    public IReadOnlyList<CategoryColorSwatchViewModel> ColorSwatches { get; }

    // Settable (not just from within this class) so the parent can cancel an in-progress rename
    // when the user switches attention to a different box instead of committing this one.
    public bool IsRenaming
    {
        get => isRenaming;
        set => SetProperty(ref isRenaming, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    // Set by the parent when Select mode toggles - gates whether tapping/dragging this box means
    // "toggle selection" or the normal color-picker/drag-to-merge behavior.
    public bool IsSelectModeActive
    {
        get => isSelectModeActive;
        set => SetProperty(ref isSelectModeActive, value);
    }

    public string EditName
    {
        get => editName;
        set => SetProperty(ref editName, value);
    }

    public ICommand TapCommand { get; }

    public ICommand StartDragCommand { get; }

    public ICommand DropCommand { get; }

    public ICommand StartRenameCommand { get; }

    public ICommand CommitRenameCommand { get; }

    private async Task CommitRenameAsync(Func<CategoryBoxViewModel, string, Task> onRename)
    {
        IsRenaming = false;
        if (string.IsNullOrWhiteSpace(EditName) || EditName.Trim() == Name)
        {
            EditName = Name;
            return;
        }

        await onRename(this, EditName.Trim());
    }
}
