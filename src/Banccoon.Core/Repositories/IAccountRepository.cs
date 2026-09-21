using Banccoon.Core.Models;

namespace Banccoon.Core.Repositories;

public interface IAccountRepository
{
    Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(Account account, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
