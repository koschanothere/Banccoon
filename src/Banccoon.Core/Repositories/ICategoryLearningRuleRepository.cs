using Banccoon.Core.Statements;

namespace Banccoon.Core.Repositories;

public interface ICategoryLearningRuleRepository
{
    Task<IReadOnlyList<CategoryLearningRule>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CategoryLearningRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(CategoryLearningRule rule, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
