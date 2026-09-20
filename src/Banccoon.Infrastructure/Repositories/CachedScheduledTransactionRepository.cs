using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Caching;

namespace Banccoon.Infrastructure.Repositories;

// See CachedAccountRepository for the caching approach - decorates the real SQLite repository.
public sealed class CachedScheduledTransactionRepository : IScheduledTransactionRepository
{
    private readonly IScheduledTransactionRepository inner;
    private readonly EntityCache<ScheduledTransaction> cache = new();

    public CachedScheduledTransactionRepository(IScheduledTransactionRepository inner)
    {
        this.inner = inner;
    }

    public async Task<IReadOnlyList<ScheduledTransaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetSnapshot(out var snapshot))
        {
            return snapshot;
        }

        return cache.Load(await inner.GetAllAsync(cancellationToken));
    }

    public async Task<ScheduledTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        return all.FirstOrDefault(scheduledTransaction => scheduledTransaction.Id == id);
    }

    public async Task SaveAsync(ScheduledTransaction scheduledTransaction, CancellationToken cancellationToken = default)
    {
        await inner.SaveAsync(scheduledTransaction, cancellationToken);
        cache.Upsert(scheduledTransaction, existing => existing.Id == scheduledTransaction.Id);
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
