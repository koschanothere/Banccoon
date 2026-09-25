using Banccoon.Core.Abstractions;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;
using Banccoon.Infrastructure.Caching;

namespace Banccoon.Infrastructure.Repositories;

// See CachedAccountRepository for the caching approach - decorates the real SQLite repository.
// This is the highest-traffic one: Dashboard, Transactions and Analytics all read transactions on
// every visit. Since 2026-09-25 only the recent months are kept (RecentTransactionWindow: this
// month and the eleven before, plus anything dated later) - years of history shouldn't all sit in
// memory for pages that show a week or a month. A range reaching further back reads the older part
// from the database each time and doesn't keep it; GetAllAsync (backups) always goes to the database.
public sealed class CachedTransactionRepository : ITransactionRepository
{
    private readonly ITransactionRepository inner;
    private readonly IDateProvider dateProvider;
    private readonly EntityCache<Transaction> cache = new();
    private readonly object windowGate = new();

    // The window start the cache was filled for; a new month moves the window and refills it.
    private DateOnly? cachedFrom;

    public CachedTransactionRepository(ITransactionRepository inner, IDateProvider dateProvider)
    {
        this.inner = inner;
        this.dateProvider = dateProvider;
    }

    public Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return inner.GetAllAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetInRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var (windowStart, recent) = await GetRecentAsync(cancellationToken);
        var result = recent.Where(transaction => transaction.Date >= from && transaction.Date <= to).ToList();
        if (from < windowStart)
        {
            var olderEnd = to < windowStart ? to : windowStart.AddDays(-1);
            result.AddRange(await inner.GetInRangeAsync(from, olderEnd, cancellationToken));
        }

        return result.OrderByDescending(transaction => transaction.Date).ToList();
    }

    public Task<DateOnly?> GetEarliestDateAsync(CancellationToken cancellationToken = default)
    {
        return inner.GetEarliestDateAsync(cancellationToken);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (_, recent) = await GetRecentAsync(cancellationToken);
        return recent.FirstOrDefault(transaction => transaction.Id == id)
            ?? await inner.GetByIdAsync(id, cancellationToken);
    }

    public async Task SaveAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await inner.SaveAsync(transaction, cancellationToken);
        if (transaction.Date >= RecentTransactionWindow.StartFor(dateProvider.Today))
        {
            cache.Upsert(transaction, existing => existing.Id == transaction.Id);
        }
        else
        {
            // An edit can move a transaction out of the window.
            cache.Remove(existing => existing.Id == transaction.Id);
        }
    }

    public async Task ReassignCategoryAsync(Guid fromCategoryId, Guid toCategoryId, CancellationToken cancellationToken = default)
    {
        await inner.ReassignCategoryAsync(fromCategoryId, toCategoryId, cancellationToken);
        cache.UpdateWhere(
            transaction => transaction.CategoryId == fromCategoryId,
            transaction => transaction with { CategoryId = toCategoryId });
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

    private async Task<(DateOnly WindowStart, IReadOnlyList<Transaction> Recent)> GetRecentAsync(CancellationToken cancellationToken)
    {
        var windowStart = RecentTransactionWindow.StartFor(dateProvider.Today);
        lock (windowGate)
        {
            if (cachedFrom == windowStart && cache.TryGetSnapshot(out var snapshot))
            {
                return (windowStart, snapshot);
            }
        }

        var loaded = await inner.GetInRangeAsync(windowStart, DateOnly.MaxValue, cancellationToken);
        lock (windowGate)
        {
            if (cachedFrom == windowStart && cache.TryGetSnapshot(out var raced))
            {
                // Another caller filled it while this one was reading - keep theirs.
                return (windowStart, raced);
            }

            cachedFrom = windowStart;
            return (windowStart, cache.Replace(loaded));
        }
    }
}
