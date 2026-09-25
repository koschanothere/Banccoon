using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Banccoon.App.Localization;
using Banccoon.App.ViewModels;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Banccoon.Tests.Infrastructure;
using Xunit;

// 2026-09-25: the import's linking step for new bank categories, Settings -> Bank categories, and
// the bank category shown on review rows.
public sealed class BankCategoryViewModelTests
{
    private const string Sber = "sberbank-debit-card-pdf";

    [Fact]
    public async Task LinkingStep_LinksPicked_IgnoresSkippedAndUntouched_AndCreatesANewCategoryOnce()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var food = new Category(Guid.NewGuid(), "Food");
        await store.Categories.SaveAsync(food);
        var service = new BankCategoryService(store.BankCategoryLinks);
        var step = new StatementBankCategoriesViewModel(service, store.Categories);

        Assert.True(await step.LoadAsync(Statement("Супермаркеты", "Супермаркеты", "Такси", "Кафе", "Фастфуд", "Прочее", null)));
        Assert.Equal(["Супермаркеты", "Кафе", "Прочее", "Такси", "Фастфуд"], step.Rows.Select(r => r.Name));
        Assert.Equal(Translator.GetPlural("StatementImport_GroupCount", 2), step.Rows[0].CountText);
        var rows = step.Rows.ToDictionary(r => r.Name);
        var createNew = step.CategoryOptions.Single(o => o.IsCreateNew);
        rows["Супермаркеты"].Category = step.CategoryOptions.Single(o => o.Name == "Food");
        rows["Такси"].SkipCommand.Execute(null);
        rows["Кафе"].Category = createNew;
        rows["Кафе"].NewCategoryName = "Eating out";
        rows["Фастфуд"].Category = createNew;
        rows["Фастфуд"].NewCategoryName = " eating OUT ";

        await step.SaveAsync(Sber);

        var eatingOut = Assert.Single(await store.Categories.GetAllAsync(), c => c.Name == "Eating out");
        var links = (await service.GetLinksAsync(Sber)).ToDictionary(l => l.BankCategory, l => l.CategoryId);
        Assert.Equal(food.Id, links["Супермаркеты"]);
        Assert.Equal(eatingOut.Id, links["Кафе"]);
        Assert.Equal(eatingOut.Id, links["Фастфуд"]);
        Assert.Null(links["Такси"]);
        Assert.Null(links["Прочее"]);

        // Nothing new the next time: the step isn't shown again for the same statement.
        Assert.False(await step.LoadAsync(Statement("Супермаркеты", "Такси", "Прочее")));
    }

    [Fact]
    public async Task Settings_ShowsOnlyBanksWithCategories_AndSavesEachChange()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var food = new Category(Guid.NewGuid(), "Food");
        await store.Categories.SaveAsync(food);
        var service = new BankCategoryService(store.BankCategoryLinks);
        var registry = new StatementParserRegistry([]);
        var settings = new BankCategoryLinksViewModel(service, store.Categories, registry);

        await settings.InitializeAsync();
        Assert.False(settings.HasBanks);

        await service.SaveLinksAsync(Sber, new Dictionary<string, Guid?> { ["Супермаркеты"] = null, ["Такси"] = food.Id });
        await service.SaveLinksAsync("other-bank", new Dictionary<string, Guid?> { ["Groceries"] = null });
        await settings.InitializeAsync();

        Assert.True(settings.HasMultipleBanks);
        settings.SelectedBank = settings.Banks.Single(b => b.ParserId == Sber);
        await WaitForAsync(() => settings.Rows.Count == 2);
        var supermarkets = settings.Rows.Single(r => r.Name == "Супермаркеты");
        var taxi = settings.Rows.Single(r => r.Name == "Такси");
        Assert.True(supermarkets.Category!.IsNone);
        Assert.Equal(food.Id, taxi.Category!.Id);

        supermarkets.Category = settings.CategoryOptions.Single(o => o.Name == "Food");
        taxi.Category = settings.CategoryOptions.Single(o => o.IsNone);
        await WaitForAsync(async () => (await service.GetLinksAsync(Sber)).All(l => l.BankCategory == "Супермаркеты" ? l.CategoryId == food.Id : l.CategoryId is null));
    }

    [Fact]
    public async Task Settings_NewCategory_IsLinked_AndOtherRowsKeepTheirPicks()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var food = new Category(Guid.NewGuid(), "Food");
        await store.Categories.SaveAsync(food);
        var service = new BankCategoryService(store.BankCategoryLinks);
        await service.SaveLinksAsync(Sber, new Dictionary<string, Guid?> { ["Супермаркеты"] = food.Id, ["Кафе"] = null });
        var settings = new BankCategoryLinksViewModel(service, store.Categories, new StatementParserRegistry([]));
        await settings.InitializeAsync();
        var supermarkets = settings.Rows.Single(r => r.Name == "Супермаркеты");
        var cafe = settings.Rows.Single(r => r.Name == "Кафе");
        foreach (var row in settings.Rows)
        {
            _ = new PickerSimulator(settings.CategoryOptions, row);
        }

        cafe.Category = settings.CategoryOptions.Single(o => o.IsCreateNew);
        cafe.NewCategoryName = "Cafes";
        cafe.CreateCategoryCommand.Execute(null);
        await WaitForAsync(() => cafe.Category?.Name == "Cafes");

        Assert.Equal("Food", supermarkets.Category?.Name);
        var cafes = Assert.Single(await store.Categories.GetAllAsync(), c => c.Name == "Cafes");
        await WaitForAsync(async () => (await service.GetLinksAsync(Sber)).Single(l => l.BankCategory == "Кафе").CategoryId == cafes.Id);
        Assert.Equal(food.Id, (await service.GetLinksAsync(Sber)).Single(l => l.BankCategory == "Супермаркеты").CategoryId);
    }

    [Fact]
    public async Task ReviewRows_ShowTheBankCategory_AndALinkedOneLandsInReady()
    {
        await using var fixture = await ReviewFixture.CreateAsync(
            [
                new ParsedStatementRow(new DateOnly(2026, 6, 1), 10m, TransactionType.Expense, "Purchase", "Magnit", BankCategory: "Супермаркеты"),
                new ParsedStatementRow(new DateOnly(2026, 6, 2), 10m, TransactionType.Expense, "Purchase", "Kiosk")
            ],
            seed: async (store, _) =>
            {
                var food = (await store.Categories.GetAllAsync()).Single(c => c.Name == "Food");
                await new BankCategoryService(store.BankCategoryLinks).SaveLinksAsync("fake", new Dictionary<string, Guid?> { ["Супермаркеты"] = food.Id });
            });
        var (magnit, kiosk) = (fixture.Review.Rows[0], fixture.Review.Rows[1]);

        Assert.Equal(string.Format(Translator.Get("StatementImport_BankCategoryFormat"), "Супермаркеты"), magnit.BankCategoryText);
        Assert.False(kiosk.HasBankCategory);
        Assert.Equal("Food", magnit.Category?.Name);
        Assert.Equal(StatementImportRowSection.Ready, magnit.Section);
    }

    private static ParsedStatement Statement(params string?[] bankCategories) => new(
        Sber,
        "Sberbank debit card PDF",
        "statement.pdf",
        bankCategories.Select((category, i) => new ParsedStatementRow(new DateOnly(2026, 6, 1).AddDays(i), 10m, TransactionType.Expense, "Purchase", $"Shop {i}", BankCategory: category)).ToList());

    private static async Task WaitForAsync(Func<bool> condition) => await WaitForAsync(() => Task.FromResult(condition()));

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 200; i++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(await condition());
    }

    // MAUI 10 Picker insert behaviour (see ImportCategoriesAndLearningTests) for a Settings row.
    private sealed class PickerSimulator
    {
        private int selectedIndex;

        public PickerSimulator(ObservableCollection<CategoryOptionViewModel> options, BankCategoryLinkRowViewModel row)
        {
            selectedIndex = row.Category is null ? -1 : options.IndexOf(row.Category);
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BankCategoryLinkRowViewModel.Category))
                {
                    selectedIndex = row.Category is null ? -1 : options.IndexOf(row.Category);
                }
            };
            options.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex <= selectedIndex)
                {
                    row.Category = options[selectedIndex];
                }
            };
        }
    }
}
