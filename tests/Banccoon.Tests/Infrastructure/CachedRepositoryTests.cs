using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Repositories;
using Xunit;

namespace Banccoon.Tests.Infrastructure;

// These exercise the Cached*Repository decorators directly against a call-counting fake inner
// repository - existing SqliteRepositoryTests already prove the real repositories are correct,
// but wouldn't catch a caching bug (e.g. "always re-fetches from the database"), since the
// returned data would still be correct, just not actually cached.
public sealed class CachedRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_OnlyHitsInnerRepositoryOnce()
    {
        var account = NewAccount("Checking");
        var inner = new FakeAccountRepository([account]);
        var cached = new CachedAccountRepository(inner);

        await cached.GetAllAsync();
        await cached.GetAllAsync();
        var result = await cached.GetAllAsync();

        Assert.Equal(1, inner.GetAllCallCount);
        Assert.Single(result);
        Assert.Equal("Checking", result[0].Name);
    }

    [Fact]
    public async Task SaveAsync_UpdatesCacheWithoutRefetchingFromInner()
    {
        var account = NewAccount("Checking");
        var inner = new FakeAccountRepository([account]);
        var cached = new CachedAccountRepository(inner);
        await cached.GetAllAsync();

        var renamed = account with { Name = "Renamed" };
        await cached.SaveAsync(renamed);
        var result = await cached.GetAllAsync();

        Assert.Equal(1, inner.GetAllCallCount);
        Assert.Single(result);
        Assert.Equal("Renamed", result[0].Name);
    }

    [Fact]
    public async Task SaveAsync_NewEntity_AppearsInSubsequentReads()
    {
        var inner = new FakeAccountRepository([]);
        var cached = new CachedAccountRepository(inner);
        await cached.GetAllAsync();

        var newAccount = NewAccount("Savings");
        await cached.SaveAsync(newAccount);
        var result = await cached.GetAllAsync();

        Assert.Equal(1, inner.GetAllCallCount);
        Assert.Single(result);
        Assert.Equal("Savings", result[0].Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFromCacheWithoutRefetchingFromInner()
    {
        var account = NewAccount("Checking");
        var inner = new FakeAccountRepository([account]);
        var cached = new CachedAccountRepository(inner);
        await cached.GetAllAsync();

        await cached.DeleteAsync(account.Id);
        var result = await cached.GetAllAsync();

        Assert.Equal(1, inner.GetAllCallCount);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_ServedFromCache_DoesNotHitInner()
    {
        var account = NewAccount("Checking");
        var inner = new FakeAccountRepository([account]);
        var cached = new CachedAccountRepository(inner);

        var found = await cached.GetByIdAsync(account.Id);
        var missing = await cached.GetByIdAsync(Guid.NewGuid());

        Assert.Equal(1, inner.GetAllCallCount);
        Assert.NotNull(found);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSnapshot_ExternalMutationDoesNotAffectCache()
    {
        var account = NewAccount("Checking");
        var inner = new FakeAccountRepository([account]);
        var cached = new CachedAccountRepository(inner);

        var firstRead = await cached.GetAllAsync();
        await cached.SaveAsync(NewAccount("Savings"));
        var secondRead = await cached.GetAllAsync();

        Assert.Single(firstRead);
        Assert.Equal(2, secondRead.Count);
    }

    [Fact]
    public async Task SettingsRepository_GetAsync_OnlyHitsInnerOnce()
    {
        var inner = new FakeSettingsRepository(NewSettings("EUR"));
        var cached = new CachedSettingsRepository(inner);

        await cached.GetAsync();
        var settings = await cached.GetAsync();

        Assert.Equal(1, inner.GetCallCount);
        Assert.NotNull(settings);
    }

    [Fact]
    public async Task SettingsRepository_SaveAsync_UpdatesCacheWithoutRefetching()
    {
        var inner = new FakeSettingsRepository(NewSettings("EUR"));
        var cached = new CachedSettingsRepository(inner);
        await cached.GetAsync();

        await cached.SaveAsync(NewSettings("USD"));
        var settings = await cached.GetAsync();

        Assert.Equal(1, inner.GetCallCount);
        Assert.Equal("USD", settings.DefaultCurrency);
    }

    private static AppSettings NewSettings(string currency) =>
        new(currency, ForecastPeriod.ThirtyDays, ReminderFrequency.Weekly);

    private static Account NewAccount(string name) => new(
        Guid.NewGuid(),
        name,
        AccountType.DebitCard,
        0m,
        "EUR",
        DateTimeOffset.UtcNow);

    private sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly List<Account> accounts;

        public FakeAccountRepository(IEnumerable<Account> seed)
        {
            accounts = seed.ToList();
        }

        public int GetAllCallCount { get; private set; }

        public Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            GetAllCallCount++;
            return Task.FromResult<IReadOnlyList<Account>>(accounts.ToList());
        }

        public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not expected to be called - CachedAccountRepository serves this from its own cache.");
        }

        public Task SaveAsync(Account account, CancellationToken cancellationToken = default)
        {
            accounts.RemoveAll(existing => existing.Id == account.Id);
            accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            accounts.RemoveAll(existing => existing.Id == id);
            return Task.CompletedTask;
        }

        public Task DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            accounts.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private AppSettings settings;

        public FakeSettingsRepository(AppSettings seed)
        {
            settings = seed;
        }

        public int GetCallCount { get; private set; }

        public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            return Task.FromResult(settings);
        }

        public Task SaveAsync(AppSettings newSettings, CancellationToken cancellationToken = default)
        {
            settings = newSettings;
            return Task.CompletedTask;
        }
    }
}
