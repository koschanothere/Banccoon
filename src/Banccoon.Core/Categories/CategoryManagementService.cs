using Banccoon.Core.Repositories;

namespace Banccoon.Core.Categories;

public sealed class CategoryManagementService : ICategoryManagementService
{
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IBankCategoryLinkRepository bankCategoryLinkRepository;

    public CategoryManagementService(
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        IBankCategoryLinkRepository bankCategoryLinkRepository)
    {
        this.categoryRepository = categoryRepository;
        this.transactionRepository = transactionRepository;
        this.bankCategoryLinkRepository = bankCategoryLinkRepository;
    }

    public async Task MergeAsync(Guid sourceCategoryId, Guid targetCategoryId, CancellationToken cancellationToken = default)
    {
        if (sourceCategoryId == targetCategoryId)
        {
            throw new ArgumentException("Cannot merge a category into itself.", nameof(targetCategoryId));
        }

        // Every transaction in the category, however old - not just the months kept in memory.
        await transactionRepository.ReassignCategoryAsync(sourceCategoryId, targetCategoryId, cancellationToken);
        // Bank categories linked to the old one follow it (deleting it would otherwise unlink them).
        await bankCategoryLinkRepository.ReassignCategoryAsync(sourceCategoryId, targetCategoryId, cancellationToken);

        await categoryRepository.DeleteAsync(sourceCategoryId, cancellationToken);
    }
}
