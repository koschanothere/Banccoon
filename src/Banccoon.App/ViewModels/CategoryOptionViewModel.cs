using Banccoon.App.Formatting;
using Banccoon.App.Localization;
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
    private CategoryOptionViewModel(Guid id, string name, Color? color, bool isCreateNew, bool isNone = false)
    {
        Id = id;
        Name = name;
        Color = color;
        IsCreateNew = isCreateNew;
        IsNone = isNone;
    }

    public static CategoryOptionViewModel ForCategory(Category category) =>
        new(category.Id, category.Name, CategoryColorPalette.GetColorForCategory(category.ColorSourceId, category.Color), isCreateNew: false);

    public static CategoryOptionViewModel CreateNewSentinel() =>
        new(Guid.Empty, Translator.Get("Common_NewCategoryOption"), color: null, isCreateNew: true);

    // "Not linked", in the Settings bank-category pickers: choosing no category is a real choice
    // there, so it needs an entry of its own (IsNone), unlike an empty picker elsewhere.
    public static CategoryOptionViewModel NotLinked() =>
        new(Guid.Empty, Translator.Get("Settings_NotLinkedOption"), color: null, isCreateNew: false, isNone: true);

    // "No subcategory", heading a SubcategoryPickerViewModel's list: keeps the parent itself.
    // (A filter says "All of the category" instead - see SubcategoryPickerViewModel.)
    public static CategoryOptionViewModel NoSubcategory(string textKey = "Common_NoSubcategoryOption") =>
        new(Guid.Empty, Translator.Get(textKey), color: null, isCreateNew: false, isNone: true);

    public Guid Id { get; }

    public string Name { get; }

    public Color? Color { get; }

    public bool IsCreateNew { get; }

    public bool IsNone { get; }

    // A real category (not "+ New category", not "Not linked").
    public bool IsCategory => !IsCreateNew && !IsNone;

    public override string ToString() => Name;
}
