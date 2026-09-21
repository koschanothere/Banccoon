using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Appearance;
using Banccoon.Core.Models;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

public sealed class CategoryBoxViewModel : ViewModelBase
{
    private readonly Func<Guid, CategoryColor, Task> onSetColor;
    private bool isSelected;
    private bool isSelectModeActive;
    private Color color = Colors.Transparent;
    private IReadOnlyList<CategoryColorSwatchViewModel> colorSwatches = Array.Empty<CategoryColorSwatchViewModel>();

    public CategoryBoxViewModel(
        Category category,
        Action<CategoryBoxViewModel> onTap,
        Action<CategoryBoxViewModel> onStartDrag,
        Action<CategoryBoxViewModel> onDrop,
        Func<Guid, CategoryColor, Task> onSetColor)
    {
        Id = category.Id;
        Name = category.Name;
        this.onSetColor = onSetColor;
        ApplyColor(category.Color);

        TapCommand = new RelayCommand(() => onTap(this));
        StartDragCommand = new RelayCommand(() => onStartDrag(this));
        DropCommand = new RelayCommand(() => onDrop(this));
    }

    public Guid Id { get; }

    public string Name { get; }

    public Color Color
    {
        get => color;
        private set => SetProperty(ref color, value);
    }

    public IReadOnlyList<CategoryColorSwatchViewModel> ColorSwatches
    {
        get => colorSwatches;
        private set => SetProperty(ref colorSwatches, value);
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

    public ICommand TapCommand { get; }

    public ICommand StartDragCommand { get; }

    public ICommand DropCommand { get; }

    // Refreshes this box's color/swatches in place from newer category data, instead of the box
    // being discarded and recreated - see CategoryManagementViewModel.ReconcileBoxes, which relies
    // on reusing box instances for categories whose data hasn't changed. Boxes are bound via a
    // BindableLayout (see SettingsPage.xaml), which isn't virtualized like CollectionView - it
    // builds a real native view the instant an item is added, so recreating every box on every
    // Settings visit meant rebuilding every category's native view from scratch even when nothing
    // about it had changed. SetProperty's equality check means this is a no-op when the color is
    // actually unchanged.
    public void UpdateColor(CategoryColor? explicitColor)
    {
        ApplyColor(explicitColor);
    }

    private void ApplyColor(CategoryColor? explicitColor)
    {
        Color = CategoryColorPalette.GetColorForCategory(Id, explicitColor);
        ColorSwatches = CategoryColorPalette.AllColors
            .Select(swatchColor => new CategoryColorSwatchViewModel(swatchColor, explicitColor == swatchColor, selectedColor => _ = onSetColor(Id, selectedColor)))
            .ToList();
    }
}
