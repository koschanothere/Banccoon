using Banccoon.Core.Models;

namespace Banccoon.Core.Repositories;

public interface ITransactionRepository
{
    // Every transaction ever recorded, straight from the database - for backups. Not cached: pages
    // read the dates they show with GetInRangeAsync instead.
    Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default);

    // Transactions dated from..to (inclusive), newest first. The recent months come from memory
    // (see RecentTransactionWindow); anything older is read from the database each time.
    Task<IReadOnlyList<Transaction>> GetInRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    // The date of the oldest transaction, or null when there are none - so a page knows whether
    // there's anything earlier to offer.
    Task<DateOnly?> GetEarliestDateAsync(CancellationToken cancellationToken = default);

    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(Transaction transaction, CancellationToken cancellationToken = default);

    // Moves every transaction in one category to another, however old - one update, not a read of
    // all history (merging categories).
    Task ReassignCategoryAsync(Guid fromCategoryId, Guid toCategoryId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
