using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Caching;

namespace Banccoon.Infrastructure.Repositories;

// See CachedAccountRepository for the caching approach - decorates the real SQLite repository.
public sealed class CachedSavingsGoalRepository : ISavingsGoalRepository
{
    private readonly ISavingsGoalRepository inner;
    private readonly EntityCache<SavingsGoal> cache = new();

    public CachedSavingsGoalRepository(ISavingsGoalRepository inner)
    {
        this.inner = inner;
    }

    public async Task<IReadOnlyList<SavingsGoal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetSnapshot(out var snapshot))
        {
            return snapshot;
        }

        return cache.Load(await inner.GetAllAsync(cancellationToken));
    }

    public async Task<SavingsGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        return all.FirstOrDefault(savingsGoal => savingsGoal.Id == id);
    }

    public async Task SaveAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken = default)
    {
        await inner.SaveAsync(savingsGoal, cancellationToken);
        cache.Upsert(savingsGoal, existing => existing.Id == savingsGoal.Id);
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
