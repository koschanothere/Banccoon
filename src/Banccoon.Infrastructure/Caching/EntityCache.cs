namespace Banccoon.Infrastructure.Caching;

// Backs the Cached*Repository decorators (see Repositories/Cached*.cs). The whole local database
// is a few hundred KB - every page's own OnAppearing was re-querying SQLite for the same tables
// on every single visit, which is most of why switching tabs felt slow. Loading each table once
// and keeping it here means every read after the first is served from memory; writes go through
// SaveAsync/DeleteAsync on the owning Cached*Repository, which patches this cache immediately so
// every other reader (any other page, since repositories are DI singletons) sees the change on
// its next read without needing a push/event mechanism.
//
// Lock-protected because RefreshFavoritesAsync and a page's own InitializeAsync commonly run as
// concurrent fire-and-forget tasks on navigation - two callers can race to populate a cold cache,
// or one can read while another writes. GetAllAsync's snapshot copy on every call means callers
// never hold a reference to the list this class mutates in place.
internal sealed class EntityCache<TEntity>
{
    private readonly object gate = new();
    private List<TEntity>? items;

    public bool TryGetSnapshot(out IReadOnlyList<TEntity> snapshot)
    {
        lock (gate)
        {
            if (items is null)
            {
                snapshot = [];
                return false;
            }

            snapshot = items.ToList();
            return true;
        }
    }

    public IReadOnlyList<TEntity> Load(IEnumerable<TEntity> loaded)
    {
        lock (gate)
        {
            // Another caller may have already populated the cache while this one was awaiting the
            // database read (see class remarks) - keep whichever load happened first rather than
            // clobbering it, since both loads reflect the same underlying data anyway.
            items ??= loaded.ToList();
            return items.ToList();
        }
    }

    public void Upsert(TEntity entity, Func<TEntity, bool> matchesId)
    {
        lock (gate)
        {
            if (items is null)
            {
                return;
            }

            var index = items.FindIndex(existing => matchesId(existing));
            if (index >= 0)
            {
                items[index] = entity;
            }
            else
            {
                items.Add(entity);
            }
        }
    }

    public void Remove(Func<TEntity, bool> matchesId)
    {
        lock (gate)
        {
            items?.RemoveAll(existing => matchesId(existing));
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            items = [];
        }
    }
}
