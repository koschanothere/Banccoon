using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Appearance;
using Banccoon.Core.Models;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

public sealed class CategoryBoxViewModel : ViewModelBase
{
    private bool isSelected;
    private bool isSelectModeActive;

    public CategoryBoxViewModel(
        Category category,
        Action<CategoryBoxViewModel> onTap,
        Action<CategoryBoxViewModel> onStartDrag,
        Action<CategoryBoxViewModel> onDrop,
        Func<Guid, CategoryColor, Task> onSetColor)
    {
        Id = category.Id;
        Name = category.Name;
        Color = CategoryColorPalette.GetColorForCategory(category.Id, category.Color);

        ColorSwatches = CategoryColorPalette.AllColors
            .Select(color => new CategoryColorSwatchViewModel(color, category.Color == color, selectedColor => _ = onSetColor(Id, selectedColor)))
            .ToList();

        TapCommand = new RelayCommand(() => onTap(this));
        StartDragCommand = new RelayCommand(() => onStartDrag(this));
        DropCommand = new RelayCommand(() => onDrop(this));
    }

    public Guid Id { get; }

    public string Name { get; }

    public Color Color { get; }

    public IReadOnlyList<CategoryColorSwatchViewModel> ColorSwatches { get; }

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

    public ICommand TapCommand { get; }

    public ICommand StartDragCommand { get; }

    public ICommand DropCommand { get; }
}
