namespace Banccoon.Core.Categories;

public interface ICategoryManagementService
{
    Task MergeAsync(Guid sourceCategoryId, Guid targetCategoryId, CancellationToken cancellationToken = default);
}
