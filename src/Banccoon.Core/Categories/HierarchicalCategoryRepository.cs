using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.Core.Categories;

// Decorates the category repository so every save anywhere in the app keeps the hierarchy rules
// (CategoryHierarchyRules), whichever call site does it:
// - saving a child checks its parent and copies the parent's color onto it;
// - saving a parent copies its color onto its children;
// - deleting a parent makes its children top-level (keeping their color) before it goes, so
//   nothing points at a missing parent (the database's ON DELETE SET NULL would do the same, but
//   behind the in-memory cache's back).
public sealed class HierarchicalCategoryRepository : ICategoryRepository
{
    private readonly ICategoryRepository inner;

    public HierarchicalCategoryRepository(ICategoryRepository inner)
    {
        this.inner = inner;
    }

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        inner.GetAllAsync(cancellationToken);

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.GetByIdAsync(id, cancellationToken);

    public async Task SaveAsync(Category category, CancellationToken cancellationToken = default)
    {
        var existing = await inner.GetAllAsync(cancellationToken);
        var normalized = CategoryHierarchyRules.Normalize(category, existing);
        await inner.SaveAsync(normalized, cancellationToken);

        if (normalized.ParentCategoryId is not null)
        {
            return;
        }

        foreach (var child in existing.Where(candidate => candidate.ParentCategoryId == normalized.Id && candidate.Color != normalized.Color))
        {
            await inner.SaveAsync(child with { Color = normalized.Color }, cancellationToken);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await inner.GetAllAsync(cancellationToken);
        foreach (var child in existing.Where(candidate => candidate.ParentCategoryId == id))
        {
            await inner.SaveAsync(child with { ParentCategoryId = null }, cancellationToken);
        }

        await inner.DeleteAsync(id, cancellationToken);
    }

    public Task DeleteAllAsync(CancellationToken cancellationToken = default) =>
        inner.DeleteAllAsync(cancellationToken);
}
