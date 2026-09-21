using Banccoon.Core.Models;

namespace Banccoon.Core.Repositories;

public interface ITransactionRepository
{
    Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(Transaction transaction, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
