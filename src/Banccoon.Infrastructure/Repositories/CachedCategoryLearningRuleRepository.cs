using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;
using Banccoon.Infrastructure.Caching;

namespace Banccoon.Infrastructure.Repositories;

// See CachedAccountRepository for the caching approach - decorates the real SQLite repository.
public sealed class CachedCategoryLearningRuleRepository : ICategoryLearningRuleRepository
{
    private readonly ICategoryLearningRuleRepository inner;
    private readonly EntityCache<CategoryLearningRule> cache = new();

    public CachedCategoryLearningRuleRepository(ICategoryLearningRuleRepository inner)
    {
        this.inner = inner;
    }

    public async Task<IReadOnlyList<CategoryLearningRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetSnapshot(out var snapshot))
        {
            return snapshot;
        }

        return cache.Load(await inner.GetAllAsync(cancellationToken));
    }

    public async Task<CategoryLearningRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        return all.FirstOrDefault(rule => rule.Id == id);
    }

    public async Task SaveAsync(CategoryLearningRule rule, CancellationToken cancellationToken = default)
    {
        await inner.SaveAsync(rule, cancellationToken);
        cache.Upsert(rule, existing => existing.Id == rule.Id);
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
