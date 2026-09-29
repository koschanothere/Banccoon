using Banccoon.Core.Appearance;
using Banccoon.Core.Categories;
using Banccoon.Core.Models;
using Banccoon.Tests.Infrastructure;
using Xunit;

namespace Banccoon.Tests.Categories;

public sealed class CategoryManagementServiceTests
{
    [Fact]
    public async Task MergeAsync_ReassignsTransactionsAndDeletesSourceCategory()
    {
        await using var store = new SqliteTestStore();
        var service = new CategoryManagementService(store.Categories, store.Transactions, store.BankCategoryLinks);

        var groceries = new Category(Guid.NewGuid(), "Groceries");
        var food = new Category(Guid.NewGuid(), "Food");
        await store.Categories.SaveAsync(groceries);
        await store.Categories.SaveAsync(food);

        var account = new Account(Guid.NewGuid(), "Checking", AccountType.DebitCard, 1000m, "EUR", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(account);

        var transaction = new Transaction(
            Guid.NewGuid(), new DateOnly(2026, 6, 1), 50m, account.Id, groceries.Id, null, TransactionType.Expense);
        await store.Transactions.SaveAsync(transaction);

        await service.MergeAsync(groceries.Id, food.Id);

        var reloadedTransaction = await store.Transactions.GetByIdAsync(transaction.Id);
        Assert.Equal(food.Id, reloadedTransaction!.CategoryId);

        var remainingCategories = await store.Categories.GetAllAsync();
        Assert.DoesNotContain(remainingCategories, category => category.Id == groceries.Id);
        Assert.Contains(remainingCategories, category => category.Id == food.Id);
    }

    [Fact]
    public async Task MergeAsync_SameCategory_Throws()
    {
        await using var store = new SqliteTestStore();
        var service = new CategoryManagementService(store.Categories, store.Transactions, store.BankCategoryLinks);
        var categoryId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() => service.MergeAsync(categoryId, categoryId));
    }

    [Fact]
    public async Task MergeAsync_ParentIntoAnotherParent_MovesItsChildrenUnderTheTarget()
    {
        await using var store = new SqliteTestStore();
        var (service, repository) = CreateHierarchical(store);
        var food = new Category(Guid.NewGuid(), "Food", Color: CategoryColor.Teal);
        var eating = new Category(Guid.NewGuid(), "Eating", Color: CategoryColor.Pink);
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        var cafes = new Category(Guid.NewGuid(), "Cafes", ParentCategoryId: food.Id);
        foreach (var category in new[] { food, eating, groceries, cafes })
        {
            await repository.SaveAsync(category);
        }

        await service.MergeAsync(food.Id, eating.Id);

        var all = await store.Categories.GetAllAsync();
        Assert.DoesNotContain(all, category => category.Id == food.Id);
        Assert.All(all.Where(category => category.Id != eating.Id), child =>
        {
            Assert.Equal(eating.Id, child.ParentCategoryId);
            Assert.Equal(CategoryColor.Pink, child.Color);
        });
        Assert.Null(all.Single(category => category.Id == eating.Id).ParentCategoryId);
    }

    [Fact]
    public async Task MergeAsync_ParentIntoAnotherParentsChild_MovesItsChildrenToBeSiblingsOfTheTarget()
    {
        await using var store = new SqliteTestStore();
        var (service, repository) = CreateHierarchical(store);
        var food = new Category(Guid.NewGuid(), "Food");
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        var shopping = new Category(Guid.NewGuid(), "Shopping", Color: CategoryColor.Blue);
        var supermarket = new Category(Guid.NewGuid(), "Supermarket", ParentCategoryId: shopping.Id);
        foreach (var category in new[] { food, groceries, shopping, supermarket })
        {
            await repository.SaveAsync(category);
        }

        await service.MergeAsync(food.Id, supermarket.Id);

        var movedGroceries = await store.Categories.GetByIdAsync(groceries.Id);
        Assert.Equal(shopping.Id, movedGroceries!.ParentCategoryId);
        Assert.Equal(CategoryColor.Blue, movedGroceries.Color);
        Assert.Equal(shopping.Id, (await store.Categories.GetByIdAsync(supermarket.Id))!.ParentCategoryId);
        Assert.Null(await store.Categories.GetByIdAsync(food.Id));
    }

    [Fact]
    public async Task MergeAsync_ParentIntoItsOwnChild_PromotesTheChildInItsPlace()
    {
        await using var store = new SqliteTestStore();
        var (service, repository) = CreateHierarchical(store);
        var food = new Category(Guid.NewGuid(), "Food", Color: CategoryColor.Amber);
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        var cafes = new Category(Guid.NewGuid(), "Cafes", ParentCategoryId: food.Id);
        foreach (var category in new[] { food, groceries, cafes })
        {
            await repository.SaveAsync(category);
        }

        var account = new Account(Guid.NewGuid(), "Checking", AccountType.DebitCard, 1000m, "EUR", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(account);
        var transaction = new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, 1), 50m, account.Id, food.Id, null, TransactionType.Expense);
        await store.Transactions.SaveAsync(transaction);

        await service.MergeAsync(food.Id, groceries.Id);

        var promoted = await store.Categories.GetByIdAsync(groceries.Id);
        Assert.Null(promoted!.ParentCategoryId);
        Assert.Equal(CategoryColor.Amber, promoted.Color);
        var movedCafes = await store.Categories.GetByIdAsync(cafes.Id);
        Assert.Equal(groceries.Id, movedCafes!.ParentCategoryId);
        Assert.Equal(CategoryColor.Amber, movedCafes.Color);
        Assert.Equal(groceries.Id, (await store.Transactions.GetByIdAsync(transaction.Id))!.CategoryId);
        Assert.Null(await store.Categories.GetByIdAsync(food.Id));
    }

    [Fact]
    public async Task MergeAsync_ChildIntoItsParent_OnlyMovesTransactions()
    {
        await using var store = new SqliteTestStore();
        var (service, repository) = CreateHierarchical(store);
        var food = new Category(Guid.NewGuid(), "Food");
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        var cafes = new Category(Guid.NewGuid(), "Cafes", ParentCategoryId: food.Id);
        foreach (var category in new[] { food, groceries, cafes })
        {
            await repository.SaveAsync(category);
        }

        await service.MergeAsync(groceries.Id, food.Id);

        var all = await store.Categories.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(food.Id, all.Single(category => category.Id == cafes.Id).ParentCategoryId);
    }

    private static (CategoryManagementService Service, HierarchicalCategoryRepository Repository) CreateHierarchical(SqliteTestStore store)
    {
        var repository = new HierarchicalCategoryRepository(store.Categories);
        return (new CategoryManagementService(repository, store.Transactions, store.BankCategoryLinks), repository);
    }
}
