using Banccoon.Core.Appearance;

namespace Banccoon.Core.Models;

// Categories are two levels deep: a category with no ParentCategoryId is a parent (whether or not
// it has children yet), one with a ParentCategoryId is a child and can't have children of its own.
// A child's Color always equals its parent's. Both rules are enforced when saving, by
// Categories.HierarchicalCategoryRepository (see CategoryHierarchyRules), not by the UI.
public sealed record Category(
    Guid Id,
    string Name,
    TransactionType? Type = null,
    CategoryColor? Color = null,
    Guid? ParentCategoryId = null)
{
    // The id a color is derived from when Color is unset (the hash fallback in the App's palette):
    // the parent's for a child, so a child never shows a different color from its parent.
    public Guid ColorSourceId => ParentCategoryId ?? Id;
}
