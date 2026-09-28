using Banccoon.App.Localization;
using Banccoon.App.ViewModels;
using Banccoon.Core.Appearance;
using Banccoon.Core.Categories;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Setup;
using Banccoon.Core.Statements;
using Banccoon.Infrastructure.Database;
using Banccoon.Infrastructure.ImportExport;
using Banccoon.Infrastructure.Statements;
using Banccoon.Tests.Infrastructure;
using Xunit;

// First-run setup's view models over the real FirstRunSetupService, the real hierarchy-enforcing
// category repository and a temp SQLite database.
public sealed class FirstRunSetupTests
{
    [Fact]
    public void EveryDefaultCategory_HasAnEnglishAndARussianName()
    {
        foreach (var language in new[] { "en", "ru" })
        {
            Translator.SetLanguage(language);
            foreach (var key in DefaultCategories.All.SelectMany(parent => parent.ChildKeys.Prepend(parent.Key)))
            {
                Assert.NotEqual($"DefaultCategory_{key}", Translator.Get($"DefaultCategory_{key}"));
            }
        }

        Translator.SetLanguage("ru");
        Assert.Equal("Продукты", Translator.Get("DefaultCategory_Groceries"));
        Translator.SetLanguage("en");
    }

    [Fact]
    public void Categories_StartAllTicked_AndUntickingAParentUnticksItsChildren()
    {
        Translator.SetLanguage("en");
        var categories = new SetupCategoriesViewModel(() => { });
        var all = categories.ExpenseParents.Concat(categories.IncomeParents).ToList();
        Assert.All(all.Concat(all.SelectMany(parent => parent.Children)), item => Assert.True(item.IsChecked));
        Assert.Equal(10, categories.ExpenseParents.Count);
        Assert.Equal(5, categories.IncomeParents.Count);

        var groceries = categories.ExpenseParents.Single(parent => parent.Key == "Groceries");
        groceries.IsChecked = false;
        Assert.All(groceries.Children, child => Assert.False(child.IsChecked));

        groceries.IsChecked = true;
        Assert.All(groceries.Children, child => Assert.True(child.IsChecked));

        // "Other" can't be unticked, even by "Untick all".
        var other = categories.ExpenseParents.Single(parent => parent.Key == DefaultCategories.OtherKey);
        other.IsChecked = false;
        Assert.True(other.IsChecked);
        categories.ClearAllCommand.Execute(null);
        Assert.True(other.IsChecked);
        Assert.False(groceries.IsChecked);
        Assert.Equal([other.Name], categories.BuildRequest().Select(category => category.Name));
    }

    [Fact]
    public void Categories_ATickedChildWithoutItsParent_MustBeGivenSomewhereToGo()
    {
        Translator.SetLanguage("en");
        var categories = new SetupCategoriesViewModel(() => { });
        var groceries = categories.ExpenseParents.Single(parent => parent.Key == "Groceries");
        var transport = categories.ExpenseParents.Single(parent => parent.Key == "Transport");
        groceries.IsChecked = false;
        transport.IsChecked = false;

        var market = groceries.Children.Single(child => child.Key == "Groceries_Market");
        var delivery = groceries.Children.Single(child => child.Key == "Groceries_GroceryDelivery");
        var fuel = transport.Children.Single(child => child.Key == "Transport_Fuel");
        market.IsChecked = true;
        delivery.IsChecked = true;
        fuel.IsChecked = true;

        Assert.True(market.NeedsParentChoice);
        Assert.False(categories.CanContinue);
        // Ticked main categories of the same kind (no income ones, no unticked ones), then the two extras.
        Assert.Contains(market.ParentChoices, choice => choice.Name == "Home");
        Assert.DoesNotContain(market.ParentChoices, choice => choice.Name == "Salary" || choice.Name == "Groceries" || choice.Name == "Transport");
        Assert.Equal(["New main category…", "Keep it as a main category"], market.ParentChoices.TakeLast(2).Select(choice => choice.Name));

        market.ParentChoice = market.ParentChoices.Single(choice => choice.Name == "Home");
        delivery.ParentChoice = delivery.ParentChoices.Single(choice => choice.Kind == SetupParentChoiceKind.NewParent);
        Assert.True(delivery.IsNamingNewParent);
        Assert.False(categories.CanContinue);
        delivery.NewParentName = "Food";
        fuel.ParentChoice = fuel.ParentChoices.Single(choice => choice.Kind == SetupParentChoiceKind.KeepAsMain);
        Assert.True(categories.CanContinue);

        var request = categories.BuildRequest();
        Assert.Contains("Market", request.Single(category => category.Name == "Home").Children.Select(child => child.Name));
        Assert.Equal(["Grocery delivery"], request.Single(category => category.Name == "Food").Children.Select(child => child.Name));
        Assert.Empty(request.Single(category => category.Name == "Fuel").Children);
        Assert.DoesNotContain(request, category => category.Name is "Groceries" or "Transport");
        Assert.True(request.Single(category => category.Name == "Other").IsFallback);

        // Ticking the main category again answers the question itself.
        groceries.IsChecked = true;
        Assert.False(market.NeedsParentChoice);
        Assert.Empty(market.ParentChoices);
    }

    [Fact]
    public async Task Wizard_InRussian_CreatesRussianCategories_AndSavesTheSettings()
    {
        await using var store = new SqliteTestStore();
        var (vm, setupService) = Create(store);
        await vm.InitializeAsync();
        FirstRunSetupDestination? finished = null;
        vm.Finished += destination => finished = destination;

        Assert.True(await setupService.IsSetupNeededAsync());
        Assert.True(vm.IsLanguageStep);
        vm.SetLanguageRussianCommand.Execute(null);
        Assert.Equal("Шаг 1 из 5", vm.StepText);
        Assert.Equal("RUB", vm.CurrencyText);
        Assert.True(vm.IsThemeSystemSelected);

        vm.NextCommand.Execute(null);
        Assert.True(vm.IsSettingsStep);
        vm.SetWindowMonthCommand.Execute(null);
        vm.AutoBackupFrequencyDaysText = "0";
        vm.NextCommand.Execute(null);
        Assert.True(vm.IsSettingsStep);
        Assert.Equal("Интервал копирования — целое число дней, не меньше 1.", vm.StatusText);
        vm.AutoBackupFrequencyDaysText = "14";
        vm.NextCommand.Execute(null);

        Assert.True(vm.IsCategoriesStep);
        var groceries = vm.Categories.ExpenseParents.Single(parent => parent.Key == "Groceries");
        Assert.Equal("Продукты", groceries.Name);
        groceries.IsChecked = false;
        groceries.Children[0].IsChecked = true;
        vm.NextCommand.Execute(null);
        Assert.True(vm.IsCategoriesStep);
        Assert.False(string.IsNullOrEmpty(vm.StatusText));
        groceries.Children[0].ParentChoice = groceries.Children[0].ParentChoices.Single(choice => choice.Kind == SetupParentChoiceKind.KeepAsMain);
        vm.NextCommand.Execute(null);

        Assert.True(vm.IsBanksStep);
        var sberbank = Assert.Single(vm.Banks.Banks);
        sberbank.IsSelected = true;
        vm.NextCommand.Execute(null);

        Assert.True(vm.IsStartStep);
        Assert.False(vm.ShowsNextButton);
        vm.StartEmptyCommand.Execute(null);
        await Task.Delay(500);

        Assert.Equal(FirstRunSetupDestination.Dashboard, finished);
        var categories = await store.Categories.GetAllAsync();
        Assert.Contains(categories, category => category.Name == "Супермаркет" && category.ParentCategoryId is null);
        Assert.DoesNotContain(categories, category => category.Name == "Продукты");
        var transport = categories.Single(category => category.Name == "Транспорт");
        Assert.Equal(CategoryColor.Blue, categories.Single(category => category.Name == "Топливо" && category.ParentCategoryId == transport.Id).Color);
        Assert.Equal(TransactionType.Income, categories.Single(category => category.Name == "Зарплата").Type);

        var settings = await store.Settings.GetAsync();
        Assert.Equal("ru", settings.DisplayLanguage);
        Assert.Equal("RUB", settings.DefaultCurrency);
        Assert.Equal(AppThemeMode.System, settings.ThemeMode);
        Assert.Equal(FreeToSpendWindowMode.CalendarMonth, settings.FreeToSpendWindowMode);
        Assert.Equal(14, settings.AutoBackupFrequencyDays);
        Assert.Equal(sberbank.ParserId, settings.PreferredParserIds);
        Assert.Equal(categories.Single(category => category.Name == "Прочее").Id, settings.FallbackCategoryId);
        Assert.False(await setupService.IsSetupNeededAsync());
        Translator.SetLanguage("en");
    }

    [Fact]
    public async Task Wizard_OpenedAgainFromSettings_CanBeClosedUnsaved_AndPutsTheLanguageBack()
    {
        await using var store = new SqliteTestStore();
        await store.Settings.SaveAsync((await store.Settings.GetAsync()) with { DisplayLanguage = "en" });
        var (vm, _) = Create(store);
        await vm.InitializeAsync();
        vm.CanClose = true;
        var closed = false;
        vm.Closed += () => closed = true;

        vm.SetLanguageRussianCommand.Execute(null);
        Assert.Equal("Шаг 1 из 5", vm.StepText);
        vm.CloseCommand.Execute(null);
        await Task.Delay(200);

        Assert.True(closed);
        Assert.Equal("Next", Translator.Get("Common_Next"));
        Assert.Empty(await store.Categories.GetAllAsync());
        Assert.False((await store.Settings.GetAsync()).FirstRunCompleted);
    }

    private static (FirstRunSetupViewModel Vm, FirstRunSetupService Service) Create(SqliteTestStore store)
    {
        Translator.SetLanguage("en");
        var categories = new HierarchicalCategoryRepository(store.Categories);
        var setupService = new FirstRunSetupService(store.Settings, categories, store.Accounts, store.Transactions);
        var reset = new LocalDataResetService(store.Accounts, store.Categories, store.Transactions, store.ScheduledTransactions, store.SavingsGoals, store.StatementImports, store.CategoryLearningRules, store.BankCategoryLinks);
        var export = new RepositoryExportService(store.Accounts, store.Categories, store.Transactions, store.ScheduledTransactions, store.SavingsGoals, store.Settings, store.StatementImports, store.CategoryLearningRules, store.BankCategoryLinks);
        var import = new RepositoryImportService(store.Accounts, categories, store.Transactions, store.ScheduledTransactions, store.SavingsGoals, store.Settings, new ExportValidator(), store.StatementImports, store.CategoryLearningRules, store.BankCategoryLinks, reset);
        var vm = new FirstRunSetupViewModel(
            setupService,
            store.Settings,
            new StatementParserRegistry([new SberbankDebitCardStatementParser()]),
            new JsonBackupService(export, import),
            reset,
            new StaticDatabasePathProvider(Path.Combine(Path.GetTempPath(), "unused.db")));
        return (vm, setupService);
    }
}
