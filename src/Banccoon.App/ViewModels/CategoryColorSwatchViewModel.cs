using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Appearance;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

public sealed class CategoryColorSwatchViewModel
{
    public CategoryColorSwatchViewModel(CategoryColor color, bool isSelected, Action<CategoryColor> onSelect)
    {
        ColorValue = color;
        Color = CategoryColorPalette.GetColor(color);
        IsSelected = isSelected;
        SelectCommand = new RelayCommand(() => onSelect(color));
    }

    public CategoryColor ColorValue { get; }

    public Color Color { get; }

    public bool IsSelected { get; }

    // A numeric thickness (rather than IsSelected + a bool-to-color converter) keeps the "selected"
    // ring visible without adding a new converter just for this - the ring's actual color is a
    // theme-aware AppThemeBinding applied uniformly in XAML.
    public double SelectionStrokeThickness => IsSelected ? 3 : 0;

    public ICommand SelectCommand { get; }
}
