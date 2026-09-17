using Banccoon.Core.Repositories;

namespace Banccoon.Core.Categories;

public sealed class CategoryManagementService : ICategoryManagementService
{
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;

    public CategoryManagementService(ICategoryRepository categoryRepository, ITransactionRepository transactionRepository)
    {
        this.categoryRepository = categoryRepository;
        this.transactionRepository = transactionRepository;
    }

    public async Task MergeAsync(Guid sourceCategoryId, Guid targetCategoryId, CancellationToken cancellationToken = default)
    {
        if (sourceCategoryId == targetCategoryId)
        {
            throw new ArgumentException("Cannot merge a category into itself.", nameof(targetCategoryId));
        }

        var transactions = await transactionRepository.GetAllAsync(cancellationToken);
        foreach (var transaction in transactions.Where(transaction => transaction.CategoryId == sourceCategoryId))
        {
            await transactionRepository.SaveAsync(transaction with { CategoryId = targetCategoryId }, cancellationToken);
        }

        await categoryRepository.DeleteAsync(sourceCategoryId, cancellationToken);
    }
}
