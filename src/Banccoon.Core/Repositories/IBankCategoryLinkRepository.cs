using Banccoon.Core.Statements;

namespace Banccoon.Core.Repositories;

public interface IBankCategoryLinkRepository
{
    Task<IReadOnlyList<BankCategoryLink>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BankCategoryLink>> GetByParserAsync(string parserId, CancellationToken cancellationToken = default);

    // Inserts or updates (keyed by parser + bank category, ignoring case), all in one transaction.
    Task SaveAllAsync(IReadOnlyList<BankCategoryLink> links, CancellationToken cancellationToken = default);

    // Categories being merged: links pointing at the old one follow it to the new one.
    Task ReassignCategoryAsync(Guid fromCategoryId, Guid toCategoryId, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
