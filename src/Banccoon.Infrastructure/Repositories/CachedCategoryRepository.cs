using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Caching;

namespace Banccoon.Infrastructure.Repositories;

// See CachedAccountRepository for the caching approach - decorates the real SQLite repository.
public sealed class CachedCategoryRepository : ICategoryRepository
{
    private readonly ICategoryRepository inner;
    private readonly EntityCache<Category> cache = new();

    public CachedCategoryRepository(ICategoryRepository inner)
    {
        this.inner = inner;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetSnapshot(out var snapshot))
        {
            return snapshot;
        }

        return cache.Load(await inner.GetAllAsync(cancellationToken));
    }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        return all.FirstOrDefault(category => category.Id == id);
    }

    public async Task SaveAsync(Category category, CancellationToken cancellationToken = default)
    {
        await inner.SaveAsync(category, cancellationToken);
        cache.Upsert(category, existing => existing.Id == category.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await inner.DeleteAsync(id, cancellationToken);
        cache.Remove(existing => existing.Id == id);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await inner.DeleteAllAsync(cancellationToken);
        cache.Clear();
    }
}
