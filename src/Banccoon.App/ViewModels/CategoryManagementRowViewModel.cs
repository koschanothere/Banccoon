using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Appearance;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class CategoryManagementRowViewModel : ViewModelBase
{
    private readonly Func<Guid, CategoryColor, Task> onSetColor;
    private string name;
    private CategoryColor? selectedColor;

    public CategoryManagementRowViewModel(
        Category category,
        Func<Guid, string, Task> onRename,
        Func<Guid, Task> onDelete,
        Func<Guid, CategoryColor, Task> onSetColor)
    {
        Id = category.Id;
        name = category.Name;
        selectedColor = category.Color;
        this.onSetColor = onSetColor;

        RenameCommand = new RelayCommand(() => _ = onRename(Id, Name));
        DeleteCommand = new RelayCommand(() => _ = onDelete(Id));

        ColorSwatches = [];
        RebuildSwatches();
    }

    public Guid Id { get; }

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public ObservableCollection<CategoryColorSwatchViewModel> ColorSwatches { get; }

    public ICommand RenameCommand { get; }

    public ICommand DeleteCommand { get; }

    private void RebuildSwatches()
    {
        ColorSwatches.Clear();
        foreach (var color in CategoryColorPalette.AllColors)
        {
            ColorSwatches.Add(new CategoryColorSwatchViewModel(color, color == selectedColor, SelectColor));
        }
    }

    private void SelectColor(CategoryColor color)
    {
        selectedColor = color;
        RebuildSwatches();
        _ = onSetColor(Id, color);
    }
}
