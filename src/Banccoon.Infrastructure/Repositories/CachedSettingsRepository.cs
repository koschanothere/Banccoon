using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.Infrastructure.Repositories;

// Settings is a single row, not a collection, so it doesn't need EntityCache - just the one
// cached instance, replaced on every save. Nearly every page reads settings on every visit
// (theme, currency, privacy mode, dashboard section order, ...), so this was one of the most
// frequently repeated SQLite round trips in the app.
public sealed class CachedSettingsRepository : ISettingsRepository
{
    private readonly ISettingsRepository inner;
    private readonly object gate = new();
    private AppSettings? cached;

    public CachedSettingsRepository(ISettingsRepository inner)
    {
        this.inner = inner;
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (cached is not null)
            {
                return cached;
            }
        }

        var loaded = await inner.GetAsync(cancellationToken);
        lock (gate)
        {
            cached ??= loaded;
            return cached;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await inner.SaveAsync(settings, cancellationToken);
        lock (gate)
        {
            cached = settings;
        }
    }
}
