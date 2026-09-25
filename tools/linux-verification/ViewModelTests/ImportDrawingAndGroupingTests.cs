using Banccoon.App.Localization;
using Banccoon.App.ViewModels;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Banccoon.Tests.Infrastructure;
using Xunit;

// 2026-09-25: only 20 duplicate / needs-a-decision rows are on screen at once (duplicates first),
// refilled as rows are dismissed - nothing is drawn in the background - and the ready block groups
// rows by sender name, with a category picker and "Approve all" per group.
public sealed class ImportDrawingAndGroupingTests
{
    [Fact]
    public async Task BigStatement_ShowsTwentyRowsDuplicatesFirst_AndOnlyMoreAsRowsAreDismissed()
    {
        // 35 new (days 1-35), 10 from a learned café (days 36-45), 5 already recorded (days 46-50) -
        // so showing by date alone would show only new rows.
        var rows = Enumerable.Range(1, 50).Select(day => day switch
        {
            <= 35 => Row(day, $"New shop {day}"),
            <= 45 => Row(day, "Cafe"),
            _ => Row(day, $"Recorded {day}")
        }).ToArray();
        await using var fixture = await ReviewFixture.CreateAsync(rows, seed: async (store, account) =>
        {
            await LearnAsync(store, account, "Cafe", "Food");
            for (var day = 46; day <= 50; day++)
            {
                await store.Transactions.SaveAsync(new Transaction(Guid.NewGuid(), DateOf(day), 10m + day, account.Id, null, null, TransactionType.Expense));
            }
        });
        var sections = fixture.Review.Sections;
        sections.ShowReadyRows();

        Assert.Equal(50, fixture.Review.Rows.Count);
        Assert.Equal(5, sections.DuplicateRows.Count);
        Assert.Equal(Enumerable.Range(1, 15).Select(DateOf), sections.AttentionRows.Select(r => r.Date));
        Assert.Equal(10, sections.ReadyRows.Count);
        // Headers and "approve all" count every row from the start, shown or not.
        Assert.Equal(string.Format(Translator.Get("StatementImport_AttentionHeaderFormat"), 35), sections.AttentionHeaderText);
        Assert.Equal(10, sections.CategorisedCount);
        Assert.True(sections.HasHiddenAttention);
        Assert.Equal(string.Format(Translator.Get("StatementImport_ShowMoreFormat"), 20), sections.ShowMoreAttentionText);

        // Nothing more appears on its own...
        await Task.Delay(300);
        Assert.Equal(15, sections.AttentionRows.Count);

        // ...only as rows are dismissed: the next undecided row takes the skipped duplicate's place.
        sections.DuplicateRows[0].SkipCommand.Execute(null);
        await fixture.SettleAsync();
        Assert.Equal(4, sections.DuplicateRows.Count);
        Assert.Equal(Enumerable.Range(1, 16).Select(DateOf), sections.AttentionRows.Select(r => r.Date));

        sections.ShowMoreCommand.Execute(null);
        Assert.Equal(Enumerable.Range(1, 35).Select(DateOf), sections.AttentionRows.Select(r => r.Date));
        Assert.False(sections.HasHiddenAttention);
    }

    [Fact]
    public async Task ApproveAllCategorised_BeforeEverythingIsDrawn_AlsoApprovesRowsNotOnScreenYet()
    {
        var rows = Enumerable.Range(1, 45).Select(day => Row(day, day <= 30 ? "Cafe" : $"New shop {day}")).ToArray();
        await using var fixture = await ReviewFixture.CreateAsync(rows, seed: (store, account) => LearnAsync(store, account, "Cafe", "Food"));
        var sections = fixture.Review.Sections;
        sections.ShowReadyRows();
        Assert.Equal(20, sections.ReadyRows.Count);

        sections.ApproveAllCategorisedCommand.Execute(null);
        await fixture.WaitForAsync(() => fixture.Review.Rows.Count == 15 && sections.AttentionRows.Count == 15);

        Assert.Equal(30, (await fixture.Store.Transactions.GetAllAsync()).Count);
        Assert.Empty(sections.ReadyRows);
        Assert.Empty(sections.ReadyGroups);
        Assert.Equal(15, sections.AttentionRows.Distinct().Count());
    }

    [Fact]
    public async Task GroupByName_OneGroupPerSenderAndCategory_InNameOrder_CollapsedUntilOpened()
    {
        await using var fixture = await ReviewFixture.CreateAsync(
            [Row(1, "Cafe"), Row(2, "Metro"), Row(3, "Cafe"), Row(4, "Bakery"), Row(5, "Cafe"), Row(6, "Bakery")],
            seed: async (store, account) =>
            {
                await LearnAsync(store, account, "Cafe", "Food");
                await LearnAsync(store, account, "Bakery", "Food");
                await LearnAsync(store, account, "Metro", "Fun");
            });
        var sections = fixture.Review.Sections;
        Assert.True(sections.IsGroupedByName);
        Assert.Empty(sections.ReadyGroups);
        sections.ShowReadyGroups();

        Assert.Equal(["Bakery", "Cafe", "Metro"], sections.ReadyGroups.Select(g => g.Name));
        var (bakery, cafe, metro) = (sections.ReadyGroups[0], sections.ReadyGroups[1], sections.ReadyGroups[2]);
        Assert.Equal([2, 3, 1], sections.ReadyGroups.Select(g => g.Count));
        Assert.True(cafe.IsMultiple);
        Assert.Empty(cafe.DisplayedRows);
        Assert.False(metro.IsMultiple);
        Assert.Single(metro.DisplayedRows);
        Assert.Equal("Food", cafe.Category?.Name);
        Assert.Equal(Banccoon.App.Formatting.MoneyFormat.Format(-(11m + 13m + 15m), "RUB"), cafe.TotalText);

        cafe.ToggleExpandedCommand.Execute(null);
        Assert.Equal([1, 3, 5], cafe.DisplayedRows.Select(r => r.Date.Day));

        // Only the view that's showing is drawn.
        sections.IsGroupedByName = false;
        Assert.True(sections.IsReadyListFlat);
        Assert.False(sections.IsReadyListGrouped);
        Assert.Equal(6, sections.ReadyRows.Count);
        Assert.Empty(sections.ReadyGroups);

        sections.IsGroupedByName = true;
        Assert.Same(bakery, sections.ReadyGroups[0]);
        Assert.Empty(sections.ReadyRows);
    }

    [Fact]
    public async Task GroupApproveAll_ApprovesThatGroupOnly_AndAShrunkGroupShowsItsLastRow()
    {
        await using var fixture = await ReviewFixture.CreateAsync(
            [Row(1, "Cafe"), Row(2, "Cafe"), Row(3, "Bakery"), Row(4, "Bakery")],
            seed: async (store, account) =>
            {
                await LearnAsync(store, account, "Cafe", "Food");
                await LearnAsync(store, account, "Bakery", "Fun");
            });
        var sections = fixture.Review.Sections;
        sections.ShowReadyGroups();
        var bakery = sections.ReadyGroups.Single(g => g.Name == "Bakery");
        var cafe = sections.ReadyGroups.Single(g => g.Name == "Cafe");

        cafe.ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Equal(2, (await fixture.Store.Transactions.GetAllAsync()).Count);
        Assert.Same(bakery, Assert.Single(sections.ReadyGroups));
        Assert.Equal(2, fixture.Review.Rows.Count);

        bakery.Rows[0].ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.False(bakery.IsMultiple);
        Assert.Equal(4, Assert.Single(bakery.DisplayedRows).Date.Day);
    }

    [Fact]
    public async Task RowsCategorisedByLearning_JoinTheirSendersGroup()
    {
        await using var fixture = await ReviewFixture.CreateAsync([Row(1, "Shop"), Row(2, "Shop"), Row(3, "Shop"), Row(4, "Metro")]);
        var review = fixture.Review;
        var sections = review.Sections;
        sections.ShowReadyGroups();
        Assert.Empty(sections.ReadyGroups);
        review.Rows[0].Category = review.CategoryOptions.First(o => o.Name == "Food");

        review.Rows[0].ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        var shop = Assert.Single(sections.ReadyGroups);
        Assert.Equal("Shop", shop.Name);
        Assert.Equal([2, 3], shop.Rows.Select(r => r.Date.Day));
        sections.ShowReadyRows();
        Assert.Equal([2, 3], sections.ReadyRows.Select(r => r.Date.Day));
        Assert.Equal(4, Assert.Single(sections.AttentionRows).Date.Day);
    }

    [Fact]
    public async Task GroupCategoryPicker_PutsTheCategoryOnEveryRow_AndApproveAllUsesIt()
    {
        await using var fixture = await ReviewFixture.CreateAsync(
            [Row(1, "Cafe"), Row(2, "Cafe"), Row(3, "Cafe")],
            seed: (store, account) => LearnAsync(store, account, "Cafe", "Food"));
        var review = fixture.Review;
        review.Sections.ShowReadyGroups();
        var fun = review.CategoryOptions.First(o => o.Name == "Fun");
        var cafe = Assert.Single(review.Sections.ReadyGroups);

        cafe.Category = fun;

        Assert.All(cafe.Rows, row => Assert.Same(fun, row.Category));
        cafe.ApproveCommand.Execute(null);
        await fixture.SettleAsync();
        Assert.All(await fixture.Store.Transactions.GetAllAsync(), t => Assert.Equal(fun.Id, t.CategoryId));
        Assert.Equal(3, (await fixture.Store.Transactions.GetAllAsync()).Count);
    }

    [Fact]
    public async Task GroupNewCategory_IsCreatedOnce_AndGoesOnTheWholeGroup()
    {
        await using var fixture = await ReviewFixture.CreateAsync(
            [Row(1, "Cafe"), Row(2, "Cafe")],
            seed: (store, account) => LearnAsync(store, account, "Cafe", "Food"));
        var review = fixture.Review;
        review.Sections.ShowReadyGroups();
        var cafe = Assert.Single(review.Sections.ReadyGroups);
        cafe.Category = review.CategoryOptions.Single(o => o.IsCreateNew);
        cafe.NewCategoryName = "Coffee";
        Assert.All(cafe.Rows, row => Assert.Equal("Food", row.Category?.Name));

        cafe.CreateCategoryCommand.Execute(null);
        await fixture.WaitForAsync(() => cafe.Category?.Name == "Coffee");

        Assert.False(cafe.IsCreatingNewCategory);
        Assert.All(cafe.Rows, row => Assert.Equal("Coffee", row.Category?.Name));
        Assert.Single(await fixture.Store.Categories.GetAllAsync(), c => c.Name == "Coffee");
    }

    [Fact]
    public async Task ExpandedGroup_ApproveAll_ApprovesEveryRowAndTheGroupGoes()
    {
        await using var fixture = await ReviewFixture.CreateAsync(
            [Row(1, "Cafe"), Row(2, "Cafe"), Row(3, "Cafe"), Row(4, "Metro")],
            seed: async (store, account) =>
            {
                await LearnAsync(store, account, "Cafe", "Food");
                await LearnAsync(store, account, "Metro", "Fun");
            });
        var sections = fixture.Review.Sections;
        sections.ShowReadyGroups();
        var cafe = sections.ReadyGroups.Single(g => g.Name == "Cafe");
        cafe.ToggleExpandedCommand.Execute(null);
        Assert.Equal(3, cafe.DisplayedRows.Count);

        cafe.ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Equal(3, (await fixture.Store.Transactions.GetAllAsync()).Count);
        Assert.Empty(cafe.DisplayedRows);
        Assert.Equal(["Metro"], sections.ReadyGroups.Select(g => g.Name));
        Assert.Equal(4, Assert.Single(fixture.Review.Rows).Date.Day);
    }

    [Fact]
    public async Task CreatingACategory_ThatSortsBeforeAGroupsPick_LeavesTheGroupAndItsRowsAlone()
    {
        await using var fixture = await ReviewFixture.CreateAsync(
            [Row(1, "Cafe"), Row(2, "Cafe"), Row(3, "Shop")],
            seed: (store, account) => LearnAsync(store, account, "Cafe", "Food"));
        var review = fixture.Review;
        review.Sections.ShowReadyGroups();
        var fun = review.CategoryOptions.First(o => o.Name == "Fun");
        var cafe = Assert.Single(review.Sections.ReadyGroups);
        cafe.Category = fun;
        _ = new GroupPickerSimulator(review.CategoryOptions, cafe);
        var shop = review.Sections.AttentionRows.Single();
        shop.Category = review.CategoryOptions.Single(o => o.IsCreateNew);
        shop.NewCategoryName = "Drinks";

        shop.CreateCategoryCommand.Execute(null);
        await fixture.WaitForAsync(() => shop.Category?.Name == "Drinks");

        Assert.Same(fun, cafe.Category);
        Assert.All(cafe.Rows, row => Assert.Same(fun, row.Category));
    }

    // The 2026-09-25 report: opening a group left its rows without a category, and the group's
    // "Approve all" then did nothing. Stand-in for what MAUI did: each row's picker, as the opened
    // group builds it, resets to "nothing" and pushes that through the two-way binding.
    [Fact]
    public async Task OpeningAGroup_WhosePickersResetToNothing_KeepsEveryRowsCategory_AndApproveAllWorks()
    {
        await using var fixture = await ReviewFixture.CreateAsync(
            [Row(1, "Cafe"), Row(2, "Cafe"), Row(3, "Cafe")],
            seed: (store, account) => LearnAsync(store, account, "Cafe", "Food"));
        var sections = fixture.Review.Sections;
        sections.ShowReadyGroups();
        var cafe = Assert.Single(sections.ReadyGroups);
        var handedBack = 0;
        foreach (var row in cafe.Rows)
        {
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(StatementImportRowViewModel.Category) && row.Category is not null)
                {
                    handedBack++;
                }
            };
        }

        cafe.DisplayedRows.CollectionChanged += (_, e) =>
        {
            foreach (var row in e.NewItems?.Cast<StatementImportRowViewModel>() ?? [])
            {
                row.Category = null;
            }
        };
        cafe.Category = null;
        cafe.ToggleExpandedCommand.Execute(null);
        await fixture.WaitForAsync(() => handedBack >= 3);

        Assert.Equal("Food", cafe.Category?.Name);
        Assert.All(cafe.Rows, row => Assert.Equal("Food", row.Category?.Name));
        cafe.ApproveCommand.Execute(null);
        await fixture.SettleAsync();
        Assert.Equal(3, (await fixture.Store.Transactions.GetAllAsync()).Count);
        Assert.Empty(fixture.Review.Rows);
    }

    // Same stand-in as ImportCategoriesAndLearningTests' row picker, for a group's picker: MAUI 10's
    // Picker, on an insert at or before its selected index, re-reads the item at the OLD index and
    // writes it back through the two-way SelectedItem binding.
    private sealed class GroupPickerSimulator
    {
        private int selectedIndex;

        public GroupPickerSimulator(System.Collections.ObjectModel.ObservableCollection<CategoryOptionViewModel> options, StatementImportRowGroupViewModel group)
        {
            selectedIndex = group.Category is null ? -1 : options.IndexOf(group.Category);
            group.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(StatementImportRowGroupViewModel.Category))
                {
                    selectedIndex = group.Category is null ? -1 : options.IndexOf(group.Category);
                }
            };
            options.CollectionChanged += (_, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewStartingIndex <= selectedIndex)
                {
                    group.Category = options[selectedIndex];
                }
            };
        }
    }

    // Day 1 is 1 June; days past 30 run on into July.
    private static DateOnly DateOf(int day) => new DateOnly(2026, 6, 1).AddDays(day - 1);

    private static ParsedStatementRow Row(int day, string sender) =>
        new(DateOf(day), 10m + day, TransactionType.Expense, "Purchase", sender);

    private static async Task LearnAsync(SqliteTestStore store, Account account, string sender, string categoryName)
    {
        var category = (await store.Categories.GetAllAsync()).Single(c => c.Name == categoryName);
        var now = DateTimeOffset.UtcNow;
        await store.CategoryLearningRules.SaveAsync(new CategoryLearningRule(
            Guid.NewGuid(), sender, sender.ToUpperInvariant(), TransactionType.Expense, category.Id, account.Id, null, 1, now, now));
    }
}
