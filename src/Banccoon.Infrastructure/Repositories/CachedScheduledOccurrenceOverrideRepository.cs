using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Caching;

namespace Banccoon.Infrastructure.Repositories;

// See CachedAccountRepository for the caching approach - decorates the real SQLite repository.
// No GetByIdAsync here since the underlying interface doesn't have one either.
public sealed class CachedScheduledOccurrenceOverrideRepository : IScheduledOccurrenceOverrideRepository
{
    private readonly IScheduledOccurrenceOverrideRepository inner;
    private readonly EntityCache<ScheduledOccurrenceOverride> cache = new();

    public CachedScheduledOccurrenceOverrideRepository(IScheduledOccurrenceOverrideRepository inner)
    {
        this.inner = inner;
    }

    public async Task<IReadOnlyList<ScheduledOccurrenceOverride>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetSnapshot(out var snapshot))
        {
            return snapshot;
        }

        return cache.Load(await inner.GetAllAsync(cancellationToken));
    }

    public async Task SaveAsync(ScheduledOccurrenceOverride occurrenceOverride, CancellationToken cancellationToken = default)
    {
        await inner.SaveAsync(occurrenceOverride, cancellationToken);
        cache.Upsert(occurrenceOverride, existing => existing.Id == occurrenceOverride.Id);
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
