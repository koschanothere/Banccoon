using Banccoon.Core.Appearance;
using Banccoon.Core.Categories;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Setup;
using Banccoon.Tests.Infrastructure;
using Xunit;

namespace Banccoon.Tests.Setup;

public sealed class FirstRunSetupServiceTests
{
    [Fact]
    public async Task IsSetupNeeded_OnlyForAnEmptyDatabaseThatHasntFinishedSetup()
    {
        await using var store = new SqliteTestStore();
        var service = Create(store);
        Assert.True(await service.IsSetupNeededAsync());

        // An existing install upgrading (FirstRunCompleted still false) that has data: no setup.
        await store.Categories.SaveAsync(new Category(Guid.NewGuid(), "Food"));
        Assert.False(await service.IsSetupNeededAsync());

        await using var withAccount = new SqliteTestStore();
        await withAccount.Accounts.SaveAsync(new Account(Guid.NewGuid(), "Card", AccountType.DebitCard, 0m, "RUB", DateTimeOffset.UtcNow));
        Assert.False(await Create(withAccount).IsSetupNeededAsync());

        await using var restored = new SqliteTestStore();
        await Create(restored).MarkCompletedAsync();
        Assert.False(await Create(restored).IsSetupNeededAsync());
    }

    [Fact]
    public async Task Complete_CreatesTheCategoryTree_AndSavesTheSettings()
    {
        await using var store = new SqliteTestStore();
        var service = Create(store);

        await service.CompleteAsync(Request(
            [
                new SetupCategory("Продукты", TransactionType.Expense, CategoryColor.Teal,
                    [new SetupCategory("Супермаркет", TransactionType.Expense, null, [])]),
                new SetupCategory("Прочее", TransactionType.Expense, CategoryColor.Slate,
                    [new SetupCategory("Подарки", TransactionType.Expense, null, [])], IsFallback: true),
                new SetupCategory("Зарплата", TransactionType.Income, CategoryColor.Teal, [])
            ],
            ["sberbank-debit-pdf"]));

        var categories = await store.Categories.GetAllAsync();
        var groceries = categories.Single(category => category.Name == "Продукты");
        var supermarket = categories.Single(category => category.Name == "Супермаркет");
        Assert.Equal(groceries.Id, supermarket.ParentCategoryId);
        Assert.Equal(CategoryColor.Teal, supermarket.Color);
        Assert.Equal(TransactionType.Income, categories.Single(category => category.Name == "Зарплата").Type);
        Assert.Equal(5, categories.Count);

        var settings = await store.Settings.GetAsync();
        Assert.True(settings.FirstRunCompleted);
        Assert.Equal("ru", settings.DisplayLanguage);
        Assert.Equal("RUB", settings.DefaultCurrency);
        Assert.Equal(AppThemeMode.System, settings.ThemeMode);
        Assert.Equal(DateDisplayFormat.DayMonthYear, settings.DateDisplayFormat);
        Assert.True(settings.AutoBackupEnabled);
        Assert.Equal(7, settings.AutoBackupFrequencyDays);
        Assert.Equal(FreeToSpendWindowMode.CalendarMonth, settings.FreeToSpendWindowMode);
        Assert.Equal("sberbank-debit-pdf", settings.PreferredParserIds);
        Assert.Equal(categories.Single(category => category.Name == "Прочее").Id, settings.FallbackCategoryId);
        Assert.False(await service.IsSetupNeededAsync());
    }

    [Fact]
    public async Task Complete_RunAgain_ReusesCategoriesByName_AndOnlyAddsWhatsMissing()
    {
        await using var store = new SqliteTestStore();
        var repository = new HierarchicalCategoryRepository(store.Categories);
        var existingFood = new Category(Guid.NewGuid(), "Groceries", Color: CategoryColor.Pink);
        await repository.SaveAsync(existingFood);
        var service = new FirstRunSetupService(store.Settings, repository, store.Accounts, store.Transactions);

        await service.CompleteAsync(Request(
            [
                new SetupCategory("groceries", TransactionType.Expense, CategoryColor.Teal,
                    [new SetupCategory("Market", TransactionType.Expense, null, [])]),
                new SetupCategory("Other", TransactionType.Expense, CategoryColor.Slate, [], IsFallback: true)
            ],
            []));
        await service.CompleteAsync(Request(
            [new SetupCategory("Other", TransactionType.Expense, CategoryColor.Slate, [], IsFallback: true)],
            []));

        var categories = await store.Categories.GetAllAsync();
        Assert.Equal(3, categories.Count);
        var groceries = categories.Single(category => category.Id == existingFood.Id);
        Assert.Equal(CategoryColor.Pink, groceries.Color);
        var market = categories.Single(category => category.Name == "Market");
        Assert.Equal(groceries.Id, market.ParentCategoryId);
        Assert.Equal(CategoryColor.Pink, market.Color);
        Assert.Equal("", (await store.Settings.GetAsync()).PreferredParserIds);
    }

    [Fact]
    public void Catalog_IsTwoLevels_WithUniqueKeys_AndOtherAsAnExpenseParent()
    {
        var keys = DefaultCategories.All.Select(parent => parent.Key)
            .Concat(DefaultCategories.All.SelectMany(parent => parent.ChildKeys))
            .ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Contains(DefaultCategories.All, parent => parent.Key == DefaultCategories.OtherKey);
        Assert.Equal(10, DefaultCategories.All.Count(parent => parent.Type == TransactionType.Expense));
        Assert.Equal(5, DefaultCategories.All.Count(parent => parent.Type == TransactionType.Income));
    }

    private static FirstRunSetupService Create(SqliteTestStore store) =>
        new(store.Settings, store.Categories, store.Accounts, store.Transactions);

    private static FirstRunSetupRequest Request(IReadOnlyList<SetupCategory> categories, IReadOnlyList<string> parsers) =>
        new("ru", " rub ", AppThemeMode.System, true, 7, FreeToSpendWindowMode.CalendarMonth, categories, parsers);
}
