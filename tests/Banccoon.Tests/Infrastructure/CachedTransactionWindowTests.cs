using Banccoon.Core.Abstractions;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Transactions;
using Banccoon.Infrastructure.Repositories;
using Xunit;

namespace Banccoon.Tests.Infrastructure;

// 2026-09-25: only the last 12 calendar months of transactions (plus anything dated later) stay in
// memory; older ranges are read from the database each time and not kept.
public sealed class CachedTransactionWindowTests
{
    // 25 Sep 2026 -> the window starts 1 Oct 2025.
    private static readonly DateOnly Today = new(2026, 9, 25);

    [Fact]
    public void Window_IsThisMonthAndTheElevenBefore()
    {
        Assert.Equal(new DateOnly(2025, 10, 1), RecentTransactionWindow.StartFor(Today));
        Assert.Equal(new DateOnly(2025, 2, 1), RecentTransactionWindow.StartFor(new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public async Task RecentMonths_AreReadOnce_OlderMonthsFromTheDatabaseEveryTime()
    {
        await using var store = new SqliteTestStore();
        var account = await SaveAccountAsync(store);
        var old = await SaveAsync(store, account, new DateOnly(2025, 9, 30));
        var firstInWindow = await SaveAsync(store, account, new DateOnly(2025, 10, 1));
        var recent = await SaveAsync(store, account, new DateOnly(2026, 9, 1));
        var future = await SaveAsync(store, account, new DateOnly(2026, 12, 1));
        var inner = new CountingTransactionRepository(store.Transactions);
        var cached = new CachedTransactionRepository(inner, new MovableDate(Today));

        var window = await cached.GetInRangeAsync(RecentTransactionWindow.StartFor(Today), DateOnly.MaxValue);
        await cached.GetInRangeAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Equal([future.Id, recent.Id, firstInWindow.Id], window.Select(t => t.Id));
        Assert.Equal(1, inner.RangeReads);

        var reachingBack = await cached.GetInRangeAsync(new DateOnly(2025, 9, 1), new DateOnly(2025, 10, 31));
        await cached.GetInRangeAsync(new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30));

        Assert.Equal([firstInWindow.Id, old.Id], reachingBack.Select(t => t.Id));
        // Each older read went to the database: nothing before the window was kept.
        Assert.Equal(3, inner.RangeReads);
        Assert.Equal((new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30)), inner.LastRange);
    }

    [Fact]
    public async Task GetById_FindsOlderTransactionsInTheDatabase()
    {
        await using var store = new SqliteTestStore();
        var account = await SaveAccountAsync(store);
        var old = await SaveAsync(store, account, new DateOnly(2024, 3, 3));
        var cached = new CachedTransactionRepository(store.Transactions, new MovableDate(Today));

        Assert.Equal(old, await cached.GetByIdAsync(old.Id));
    }

    [Fact]
    public async Task Saving_KeepsTheCacheToTheWindow()
    {
        await using var store = new SqliteTestStore();
        var account = await SaveAccountAsync(store);
        var cached = new CachedTransactionRepository(store.Transactions, new MovableDate(Today));
        var windowStart = RecentTransactionWindow.StartFor(Today);
        await cached.GetInRangeAsync(windowStart, DateOnly.MaxValue);

        var recent = NewTransaction(account, new DateOnly(2026, 9, 20));
        await cached.SaveAsync(recent);
        await cached.SaveAsync(NewTransaction(account, new DateOnly(2024, 1, 1)));
        Assert.Equal([recent.Id], (await cached.GetInRangeAsync(windowStart, DateOnly.MaxValue)).Select(t => t.Id));

        // Re-dated out of the window: gone from the recent months, still in the database.
        await cached.SaveAsync(recent with { Date = new DateOnly(2025, 1, 1) });
        Assert.Empty(await cached.GetInRangeAsync(windowStart, DateOnly.MaxValue));
        Assert.Equal(2, (await cached.GetAllAsync()).Count);
    }

    [Fact]
    public async Task ANewMonth_MovesTheWindow()
    {
        await using var store = new SqliteTestStore();
        var account = await SaveAccountAsync(store);
        var october = await SaveAsync(store, account, new DateOnly(2025, 10, 15));
        var date = new MovableDate(Today);
        var inner = new CountingTransactionRepository(store.Transactions);
        var cached = new CachedTransactionRepository(inner, date);
        Assert.Single(await cached.GetInRangeAsync(RecentTransactionWindow.StartFor(date.Today), DateOnly.MaxValue));

        date.Today = new DateOnly(2026, 10, 2);

        Assert.Empty(await cached.GetInRangeAsync(RecentTransactionWindow.StartFor(date.Today), DateOnly.MaxValue));
        Assert.Equal(october.Id, Assert.Single(await cached.GetInRangeAsync(new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31))).Id);
        Assert.Equal(3, inner.RangeReads);
    }

    [Fact]
    public async Task ReassignCategory_MovesOldAndRecentTransactions()
    {
        await using var store = new SqliteTestStore();
        var account = await SaveAccountAsync(store);
        var from = new Category(Guid.NewGuid(), "Cafes");
        var to = new Category(Guid.NewGuid(), "Food");
        await store.Categories.SaveAsync(from);
        await store.Categories.SaveAsync(to);
        await store.Transactions.SaveAsync(NewTransaction(account, new DateOnly(2023, 5, 5)) with { CategoryId = from.Id });
        await store.Transactions.SaveAsync(NewTransaction(account, new DateOnly(2026, 9, 5)) with { CategoryId = from.Id });
        var cached = new CachedTransactionRepository(store.Transactions, new MovableDate(Today));
        await cached.GetInRangeAsync(RecentTransactionWindow.StartFor(Today), DateOnly.MaxValue);

        await cached.ReassignCategoryAsync(from.Id, to.Id);

        Assert.All(await cached.GetAllAsync(), t => Assert.Equal(to.Id, t.CategoryId));
        Assert.All(await cached.GetInRangeAsync(RecentTransactionWindow.StartFor(Today), DateOnly.MaxValue), t => Assert.Equal(to.Id, t.CategoryId));
    }

    [Fact]
    public async Task EarliestDate_IsTheOldestTransaction()
    {
        await using var store = new SqliteTestStore();
        Assert.Null(await store.Transactions.GetEarliestDateAsync());
        var account = await SaveAccountAsync(store);
        await SaveAsync(store, account, new DateOnly(2026, 1, 1));
        await SaveAsync(store, account, new DateOnly(2022, 7, 9));

        Assert.Equal(new DateOnly(2022, 7, 9), await store.Transactions.GetEarliestDateAsync());
    }

    private static async Task<Account> SaveAccountAsync(SqliteTestStore store)
    {
        var account = new Account(Guid.NewGuid(), "Card", AccountType.DebitCard, 100m, "RUB", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(account);
        return account;
    }

    private static async Task<Transaction> SaveAsync(SqliteTestStore store, Account account, DateOnly date)
    {
        var transaction = NewTransaction(account, date);
        await store.Transactions.SaveAsync(transaction);
        return transaction;
    }

    private static Transaction NewTransaction(Account account, DateOnly date) =>
        new(Guid.NewGuid(), date, 5m, account.Id, null, null, TransactionType.Expense, Name: $"On {date}");

    private sealed class MovableDate(DateOnly today) : IDateProvider
    {
        public DateOnly Today { get; set; } = today;
    }

    private sealed class CountingTransactionRepository(ITransactionRepository inner) : ITransactionRepository
    {
        public int RangeReads { get; private set; }

        public (DateOnly From, DateOnly To) LastRange { get; private set; }

        public Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default) => inner.GetAllAsync(cancellationToken);

        public Task<IReadOnlyList<Transaction>> GetInRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        {
            RangeReads++;
            LastRange = (from, to);
            return inner.GetInRangeAsync(from, to, cancellationToken);
        }

        public Task<DateOnly?> GetEarliestDateAsync(CancellationToken cancellationToken = default) => inner.GetEarliestDateAsync(cancellationToken);

        public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetByIdAsync(id, cancellationToken);

        public Task SaveAsync(Transaction transaction, CancellationToken cancellationToken = default) => inner.SaveAsync(transaction, cancellationToken);

        public Task ReassignCategoryAsync(Guid fromCategoryId, Guid toCategoryId, CancellationToken cancellationToken = default) => inner.ReassignCategoryAsync(fromCategoryId, toCategoryId, cancellationToken);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => inner.DeleteAsync(id, cancellationToken);

        public Task DeleteAllAsync(CancellationToken cancellationToken = default) => inner.DeleteAllAsync(cancellationToken);
    }
}
