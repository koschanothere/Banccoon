using Banccoon.Core.Categories;

namespace Banccoon.App.Formatting;

public static class CategoryDisplay
{
    // "Food › Groceries" for a child category, just "Food" for a parent - for places that show a
    // stored category as one piece of text (a learned rule, a transaction row) rather than as the
    // two pickers.
    public static string PathName(CategoryTree tree, Guid categoryId)
    {
        if (tree.Find(categoryId) is not { } category)
        {
            return string.Empty;
        }

        return tree.IsChild(categoryId) && tree.Find(tree.RootIdOf(categoryId)) is { } parent
            ? $"{parent.Name} › {category.Name}"
            : category.Name;
    }
}
