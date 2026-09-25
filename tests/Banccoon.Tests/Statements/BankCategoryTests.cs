using Banccoon.Core.Categories;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Banccoon.Core.Transactions;
using Banccoon.Tests.Infrastructure;
using Xunit;

namespace Banccoon.Tests.Statements;

// 2026-09-25: a bank's own operation categories can be linked to app categories. The list per bank
// grows from its statements; a learned rule still wins; links never set the type.
public sealed class BankCategoryTests
{
    private const string Sber = "sber";

    [Fact]
    public async Task NewCategories_AreTheOnesThisBankHasNeverShown_MostUsedFirst()
    {
        await using var store = new SqliteTestStore();
        var service = new BankCategoryService(store.BankCategoryLinks);
        await service.SaveLinksAsync(Sber, new Dictionary<string, Guid?> { ["Такси"] = null });

        var detected = await service.GetNewCategoriesAsync(Statement(
            Row("Магнит", "Супермаркеты"),
            Row("Пятёрочка", "Супермаркеты"),
            Row("Лента", "супермаркеты "),
            Row("Яндекс Go", "Такси"),
            Row("Кафе", "Рестораны и кафе"),
            Row("Перевод", null)));

        Assert.Equal([new DetectedBankCategory("Супермаркеты", 3), new DetectedBankCategory("Рестораны и кафе", 1)], detected);
    }

    [Fact]
    public async Task SavingLinks_AddsNewOnes_AndUpdatesExistingOnesWithoutMovingFirstSeen()
    {
        await using var store = new SqliteTestStore();
        var food = await SaveCategoryAsync(store, "Food");
        var service = new BankCategoryService(store.BankCategoryLinks);
        await service.SaveLinksAsync(Sber, new Dictionary<string, Guid?> { ["Супермаркеты"] = null, ["Такси"] = null });
        var firstSeen = (await service.GetLinksAsync(Sber)).Single(link => link.BankCategory == "Супермаркеты").FirstSeenAt;

        await service.SaveLinksAsync(Sber, new Dictionary<string, Guid?> { ["супермаркеты"] = food.Id });

        var links = await service.GetLinksAsync(Sber);
        Assert.Equal(2, links.Count);
        var supermarkets = links.Single(link => link.BankCategory == "Супермаркеты");
        Assert.Equal(food.Id, supermarkets.CategoryId);
        Assert.Equal(firstSeen, supermarkets.FirstSeenAt);
        Assert.Equal([Sber], await service.GetParsersWithCategoriesAsync());
    }

    [Fact]
    public async Task Import_UsesALinkedBankCategory_ButALearnedRuleWins_AndSkippedOnesStayEmpty()
    {
        await using var store = new SqliteTestStore();
        var account = await SaveAccountAsync(store);
        var food = await SaveCategoryAsync(store, "Food");
        var fun = await SaveCategoryAsync(store, "Fun");
        await new BankCategoryService(store.BankCategoryLinks).SaveLinksAsync(
            Sber,
            new Dictionary<string, Guid?> { ["Супермаркеты"] = food.Id, ["Прочее"] = null });
        var now = DateTimeOffset.UtcNow;
        await store.CategoryLearningRules.SaveAsync(new CategoryLearningRule(Guid.NewGuid(), "Cafe", "CAFE", TransactionType.Expense, fun.Id, account.Id, null, 1, now, now));
        var service = CreateImportService(store);

        var pending = await service.CreatePendingImportAsync(account.Id, "statement.pdf", Statement(
            Row("Магнит", "Супермаркеты"),
            Row("Cafe", "Супермаркеты"),
            Row("Лавка", "Прочее"),
            Row("Киоск", null)));

        var rows = pending.Rows.ToDictionary(row => row.Counterparty!);
        Assert.Equal(food.Id, rows["Магнит"].SuggestedCategoryId);
        Assert.Equal(fun.Id, rows["Cafe"].SuggestedCategoryId);
        Assert.Null(rows["Лавка"].SuggestedCategoryId);
        Assert.Null(rows["Киоск"].SuggestedCategoryId);
        Assert.Equal("Супермаркеты", (await store.StatementImports.GetRowByIdAsync(rows["Магнит"].Id))!.BankCategory);
    }

    [Fact]
    public async Task ALinkMadeDuringReview_ReachesThePendingRows()
    {
        await using var store = new SqliteTestStore();
        var account = await SaveAccountAsync(store);
        var food = await SaveCategoryAsync(store, "Food");
        var service = CreateImportService(store);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.pdf", Statement(Row("Магнит", "Супермаркеты")));
        Assert.Null(Assert.Single(pending.Rows).SuggestedCategoryId);

        await new BankCategoryService(store.BankCategoryLinks).SaveLinksAsync(Sber, new Dictionary<string, Guid?> { ["Супермаркеты"] = food.Id });

        Assert.Equal(food.Id, Assert.Single(await service.GetPendingSuggestionsAsync(pending.Batch!.Id)).CategoryId);
    }

    [Fact]
    public async Task MergingCategories_MovesLinks_AndDeletingOneUnlinks()
    {
        await using var store = new SqliteTestStore();
        var groceries = await SaveCategoryAsync(store, "Groceries");
        var food = await SaveCategoryAsync(store, "Food");
        var taxi = await SaveCategoryAsync(store, "Taxi");
        var links = new BankCategoryService(store.BankCategoryLinks);
        await links.SaveLinksAsync(Sber, new Dictionary<string, Guid?> { ["Супермаркеты"] = groceries.Id, ["Такси"] = taxi.Id });

        await new CategoryManagementService(store.Categories, store.Transactions, store.BankCategoryLinks).MergeAsync(groceries.Id, food.Id);
        await store.Categories.DeleteAsync(taxi.Id);

        var saved = await links.GetLinksAsync(Sber);
        Assert.Equal(food.Id, saved.Single(link => link.BankCategory == "Супермаркеты").CategoryId);
        Assert.Null(saved.Single(link => link.BankCategory == "Такси").CategoryId);
    }

    private static ParsedStatement Statement(params ParsedStatementRow[] rows) => new(Sber, "Sberbank", "statement.pdf", rows);

    private static ParsedStatementRow Row(string counterparty, string? bankCategory) =>
        new(new DateOnly(2026, 6, 10), 10m, TransactionType.Expense, "Purchase", counterparty, BankCategory: bankCategory);

    private static async Task<Account> SaveAccountAsync(SqliteTestStore store)
    {
        var account = new Account(Guid.NewGuid(), "Card", AccountType.DebitCard, 100m, "RUB", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(account);
        return account;
    }

    private static async Task<Category> SaveCategoryAsync(SqliteTestStore store, string name)
    {
        var category = new Category(Guid.NewGuid(), name);
        await store.Categories.SaveAsync(category);
        return category;
    }

    private static StatementImportService CreateImportService(SqliteTestStore store) => new(
        new StatementParserRegistry([]),
        store.StatementImports,
        store.CategoryLearningRules,
        store.Accounts,
        store.Categories,
        store.Transactions,
        new TransactionApplicationService(new TransactionBalanceService()),
        new CategorySuggestionService(),
        store.BankCategoryLinks);
}
