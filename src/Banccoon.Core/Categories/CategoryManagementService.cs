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
        await RehomeChildrenAsync(sourceCategoryId, targetCategoryId, cancellationToken);

        await categoryRepository.DeleteAsync(sourceCategoryId, cancellationToken);
    }

    // The merged-into category takes the merged-away one's place, so the source's children move
    // to the family the target belongs to, staying children (never silently promoted):
    // - target is top-level: they become the target's children;
    // - target is a child of another parent: they join the target's parent, as its siblings
    //   (the target can't have children of its own);
    // - target is one of the source's own children (merging a parent into its child): the
    //   target is promoted to top-level in the source's place, and its former siblings become
    //   its children.
    // Each moved child takes its new parent's color, like any child.
    private async Task RehomeChildrenAsync(Guid sourceCategoryId, Guid targetCategoryId, CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var children = categories.Where(category => category.ParentCategoryId == sourceCategoryId && category.Id != targetCategoryId).ToList();
        var target = categories.FirstOrDefault(category => category.Id == targetCategoryId);
        if (target is null)
        {
            return;
        }

        var targetParent = target.ParentCategoryId is { } parentId
            ? categories.FirstOrDefault(category => category.Id == parentId)
            : null;
        if (target.ParentCategoryId is not null && (target.ParentCategoryId == sourceCategoryId || targetParent is null))
        {
            // Promoted into the source's place (or its parent is missing anyway). It keeps its
            // color, which was the source's.
            target = target with { ParentCategoryId = null };
            await categoryRepository.SaveAsync(target, cancellationToken);
            targetParent = null;
        }

        var newParent = targetParent ?? target;
        foreach (var child in children)
        {
            await categoryRepository.SaveAsync(child with { ParentCategoryId = newParent.Id, Color = newParent.Color }, cancellationToken);
        }
    }
}
