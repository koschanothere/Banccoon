using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Caching;

namespace Banccoon.Infrastructure.Repositories;

// Decorates the real SQLite repository with an in-memory cache (see EntityCache remarks) - reads
// after the first are served from memory, writes go to SQLite first and then patch the cache.
public sealed class CachedAccountRepository : IAccountRepository
{
    private readonly IAccountRepository inner;
    private readonly EntityCache<Account> cache = new();

    public CachedAccountRepository(IAccountRepository inner)
    {
        this.inner = inner;
    }

    public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetSnapshot(out var snapshot))
        {
            return snapshot;
        }

        return cache.Load(await inner.GetAllAsync(cancellationToken));
    }

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        return all.FirstOrDefault(account => account.Id == id);
    }

    public async Task SaveAsync(Account account, CancellationToken cancellationToken = default)
    {
        await inner.SaveAsync(account, cancellationToken);
        cache.Upsert(account, existing => existing.Id == account.Id);
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
