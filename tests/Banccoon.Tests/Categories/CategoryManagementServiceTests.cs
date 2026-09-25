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
}
