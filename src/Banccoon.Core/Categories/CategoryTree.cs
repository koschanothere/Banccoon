using Banccoon.Core.Models;

namespace Banccoon.Core.Categories;

// A read-only snapshot of the two-level category hierarchy, for pickers and analytics.
// Tolerant of data that breaks the rules (they're enforced on save, but a snapshot never throws):
// a category whose parent is missing, or whose parent is itself a child, is treated as top-level.
public sealed class CategoryTree
{
    private readonly Dictionary<Guid, Category> byId;
    private readonly Dictionary<Guid, List<Category>> childrenByParentId = [];

    public CategoryTree(IEnumerable<Category> categories)
    {
        byId = [];
        foreach (var category in categories)
        {
            byId[category.Id] = category;
        }

        var topLevel = new List<Category>();
        foreach (var category in byId.Values)
        {
            if (EffectiveParentId(category) is { } parentId)
            {
                if (!childrenByParentId.TryGetValue(parentId, out var children))
                {
                    children = [];
                    childrenByParentId[parentId] = children;
                }

                children.Add(category);
            }
            else
            {
                topLevel.Add(category);
            }
        }

        TopLevel = SortByName(topLevel);
        foreach (var children in childrenByParentId.Values)
        {
            children.Sort(CompareByName);
        }
    }

    public static CategoryTree Empty { get; } = new([]);

    // Every parent (top-level) category, by name.
    public IReadOnlyList<Category> TopLevel { get; }

    public Category? Find(Guid id) => byId.GetValueOrDefault(id);

    // A parent's children, by name; empty for a child, an unknown id, or a parent with none.
    public IReadOnlyList<Category> ChildrenOf(Guid parentId) =>
        childrenByParentId.TryGetValue(parentId, out var children) ? children : [];

    public bool HasChildren(Guid parentId) => childrenByParentId.ContainsKey(parentId);

    public bool IsChild(Guid id) => byId.TryGetValue(id, out var category) && EffectiveParentId(category) is not null;

    // The parent of a child, or the category itself for a parent (or an id the tree doesn't know).
    public Guid RootIdOf(Guid id) =>
        byId.TryGetValue(id, out var category) && EffectiveParentId(category) is { } parentId ? parentId : id;

    // Splits a chosen category into what a two-step picker shows: the parent in the first picker,
    // and the child (if it is one) in the second.
    public (Guid ParentId, Guid? ChildId) Split(Guid id)
    {
        var rootId = RootIdOf(id);
        return rootId == id ? (id, null) : (rootId, id);
    }

    private Guid? EffectiveParentId(Category category) =>
        category.ParentCategoryId is { } parentId
            && parentId != category.Id
            && byId.TryGetValue(parentId, out var parent)
            && parent.ParentCategoryId is null
            ? parentId
            : null;

    private static List<Category> SortByName(List<Category> categories)
    {
        categories.Sort(CompareByName);
        return categories;
    }

    private static int CompareByName(Category left, Category right) =>
        string.Compare(left.Name, right.Name, StringComparison.CurrentCulture);
}
