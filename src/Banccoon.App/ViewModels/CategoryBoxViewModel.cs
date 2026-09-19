using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Appearance;
using Banccoon.Core.Models;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

public sealed class CategoryBoxViewModel : ViewModelBase
{
    private bool isColorMenuOpen;
    private bool isRenaming;
    private string editName;

    public CategoryBoxViewModel(
        Category category,
        Action<CategoryBoxViewModel> onStartDrag,
        Action<CategoryBoxViewModel> onDrop,
        Func<CategoryBoxViewModel, string, Task> onRename,
        Func<Guid, CategoryColor, Task> onSetColor,
        Func<Guid, Task> onDelete)
    {
        Id = category.Id;
        Name = category.Name;
        Color = CategoryColorPalette.GetColorForCategory(category.Id, category.Color);
        editName = Name;

        ColorSwatches = CategoryColorPalette.AllColors
            .Select(color => new CategoryColorSwatchViewModel(color, category.Color == color, selectedColor =>
            {
                IsColorMenuOpen = false;
                _ = onSetColor(Id, selectedColor);
            }))
            .ToList();

        StartDragCommand = new RelayCommand(() => onStartDrag(this));
        DropCommand = new RelayCommand(() => onDrop(this));
        ToggleColorMenuCommand = new RelayCommand(() => IsColorMenuOpen = !IsColorMenuOpen);
        StartRenameCommand = new RelayCommand(() => IsRenaming = true);
        CommitRenameCommand = new RelayCommand(() => _ = CommitRenameAsync(onRename));
        CancelRenameCommand = new RelayCommand(() =>
        {
            EditName = Name;
            IsRenaming = false;
        });
        DeleteCommand = new RelayCommand(() => _ = onDelete(Id));
    }

    public Guid Id { get; }

    public string Name { get; }

    public Color Color { get; }

    public IReadOnlyList<CategoryColorSwatchViewModel> ColorSwatches { get; }

    public bool IsColorMenuOpen
    {
        get => isColorMenuOpen;
        set => SetProperty(ref isColorMenuOpen, value);
    }

    public bool IsRenaming
    {
        get => isRenaming;
        private set => SetProperty(ref isRenaming, value);
    }

    public string EditName
    {
        get => editName;
        set => SetProperty(ref editName, value);
    }

    public ICommand StartDragCommand { get; }

    public ICommand DropCommand { get; }

    public ICommand ToggleColorMenuCommand { get; }

    public ICommand StartRenameCommand { get; }

    public ICommand CommitRenameCommand { get; }

    public ICommand CancelRenameCommand { get; }

    public ICommand DeleteCommand { get; }

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
