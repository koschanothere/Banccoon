using Banccoon.App.Formatting;
using Banccoon.Core.Models;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

// A category option for pickers that need to both display a category's color and let the user
// create a brand new category inline, without leaving the picker. The "create new" entry is a
// sentinel with no real category behind it yet (Id is unused/never persisted) - callers detect it
// via IsCreateNew rather than by Id, since Guid.Empty is only meaningful as "there is no real
// category id here", not as an identity to compare against.
public sealed class CategoryOptionViewModel
{
    private CategoryOptionViewModel(Guid id, string name, Color? color, bool isCreateNew)
    {
        Id = id;
        Name = name;
        Color = color;
        IsCreateNew = isCreateNew;
    }

    public static CategoryOptionViewModel ForCategory(Category category) =>
        new(category.Id, category.Name, CategoryColorPalette.GetColorForCategory(category.Id, category.Color), isCreateNew: false);

    public static CategoryOptionViewModel CreateNewSentinel() =>
        new(Guid.Empty, "+ New category", color: null, isCreateNew: true);

    public Guid Id { get; }

    public string Name { get; }

    public Color? Color { get; }

    public bool IsCreateNew { get; }

    public override string ToString() => Name;
}
