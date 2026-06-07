using Banccoon.Core.Models;

namespace Banccoon.Core.Repositories;

public interface IScheduledTransactionRepository
{
    Task<IReadOnlyList<ScheduledTransaction>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ScheduledTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(ScheduledTransaction scheduledTransaction, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
