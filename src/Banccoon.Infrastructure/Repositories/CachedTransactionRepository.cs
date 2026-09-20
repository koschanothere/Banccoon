using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Caching;

namespace Banccoon.Infrastructure.Repositories;

// See CachedAccountRepository for the caching approach - decorates the real SQLite repository.
// This is the highest-traffic one: Dashboard, Transactions, and Analytics each fetched every
// transaction on every single visit (Dashboard and Analytics even fetched them independently of
// each other on the same Dashboard load), all serialized through a fresh, unpooled SQLite
// connection - the biggest single contributor to tab-switching feeling slow.
public sealed class CachedTransactionRepository : ITransactionRepository
{
    private readonly ITransactionRepository inner;
    private readonly EntityCache<Transaction> cache = new();

    public CachedTransactionRepository(ITransactionRepository inner)
    {
        this.inner = inner;
    }

    public async Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetSnapshot(out var snapshot))
        {
            return snapshot;
        }

        return cache.Load(await inner.GetAllAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        return all.Where(transaction => transaction.AccountId == accountId || transaction.DestinationAccountId == accountId).ToList();
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        return all.FirstOrDefault(transaction => transaction.Id == id);
    }

    public async Task SaveAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await inner.SaveAsync(transaction, cancellationToken);
        cache.Upsert(transaction, existing => existing.Id == transaction.Id);
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
