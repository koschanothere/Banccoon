using Banccoon.Core.Models;

namespace Banccoon.Core.Repositories;

public interface ISavingsGoalRepository
{
    Task<IReadOnlyList<SavingsGoal>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SavingsGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
