using Banccoon.Core.Models;

namespace Banccoon.Core.Categories;

// The two rules of the category hierarchy, as pure functions over the current set of categories:
// only two levels (a child's parent must be top-level, and a category other categories point to
// can't become a child), and a child's color is always its parent's.
public static class CategoryHierarchyRules
{
    // Returns the category as it must be stored (a child takes its parent's color), or throws
    // InvalidOperationException when the parent breaks the two-level rule. These are programming
    // errors, not user input: every picker only ever offers valid parents.
    public static Category Normalize(Category candidate, IReadOnlyList<Category> existing)
    {
        if (candidate.ParentCategoryId is not { } parentId)
        {
            return candidate;
        }

        if (parentId == candidate.Id)
        {
            throw new InvalidOperationException("A category can't be its own parent.");
        }

        var parent = existing.FirstOrDefault(category => category.Id == parentId)
            ?? throw new InvalidOperationException($"Parent category {parentId} doesn't exist.");
        if (parent.ParentCategoryId is not null)
        {
            throw new InvalidOperationException("A child category can't have children of its own.");
        }

        if (existing.Any(category => category.ParentCategoryId == candidate.Id && category.Id != candidate.Id))
        {
            throw new InvalidOperationException("A category with children can't become a child itself.");
        }

        return candidate with { Color = parent.Color };
    }

    // Like Normalize, but a parent that breaks the rules is dropped (the category is kept as
    // top-level) instead of throwing - for restoring a backup, where one odd category shouldn't
    // fail the whole restore.
    public static Category NormalizeOrPromote(Category candidate, IReadOnlyList<Category> existing)
    {
        try
        {
            return Normalize(candidate, existing);
        }
        catch (InvalidOperationException)
        {
            return candidate with { ParentCategoryId = null };
        }
    }

    // Parents first, so saving the list in order never saves a child before its parent.
    public static IReadOnlyList<Category> ParentsFirst(IEnumerable<Category> categories) =>
        categories.OrderBy(category => category.ParentCategoryId is null ? 0 : 1).ToList();
}
