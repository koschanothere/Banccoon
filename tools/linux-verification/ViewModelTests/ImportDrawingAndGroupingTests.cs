using Banccoon.App.ViewModels;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Banccoon.Tests.Infrastructure;
using Xunit;

// 2026-09-25: big statements are drawn 20 rows at a time (duplicates and categorised rows first),
// and the ready block groups rows by sender name.
public sealed class ImportDrawingAndGroupingTests
{
    [Fact]
    public async Task BigStatement_DrawsTwentyRowsAtOnce_DuplicatesAndReadyFirst_ThenTheRest()
    {
        // 35 new (days 1-35), 10 from a learned café (days 36-45), 5 already recorded (days 46-50) -
        // so drawing by date alone would show only new rows first.
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

        Assert.Equal(50, fixture.Review.Rows.Count);
        Assert.Equal(5, sections.DuplicateRows.Count);
        Assert.Equal(10, sections.ReadyRows.Count);
        Assert.Equal(5, sections.AttentionRows.Count);
        Assert.Equal(Enumerable.Range(1, 5).Select(DateOf), sections.AttentionRows.Select(r => r.Date));
        // Headers and "approve all" count every row from the start, drawn or not.
        Assert.Equal(string.Format(Banccoon.App.Localization.Translator.Get("StatementImport_AttentionHeaderFormat"), 35), sections.AttentionHeaderText);
        Assert.Equal(10, sections.CategorisedCount);

        await fixture.WaitForAsync(() => sections.AttentionRows.Count == 35);
        Assert.Equal(Enumerable.Range(1, 35).Select(DateOf), sections.AttentionRows.Select(r => r.Date));
    }

    [Fact]
    public async Task ApproveAllCategorised_BeforeEverythingIsDrawn_AlsoApprovesRowsNotOnScreenYet()
    {
        var rows = Enumerable.Range(1, 45).Select(day => Row(day, day <= 30 ? "Cafe" : $"New shop {day}")).ToArray();
        await using var fixture = await ReviewFixture.CreateAsync(rows, seed: (store, account) => LearnAsync(store, account, "Cafe", "Food"));
        var sections = fixture.Review.Sections;
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

        Assert.Equal(["Bakery", "Cafe", "Metro"], sections.ReadyGroups.Select(g => g.Name));
        var (bakery, cafe, metro) = (sections.ReadyGroups[0], sections.ReadyGroups[1], sections.ReadyGroups[2]);
        Assert.Equal([2, 3, 1], sections.ReadyGroups.Select(g => g.Count));
        Assert.True(cafe.IsMultiple);
        Assert.Empty(cafe.DisplayedRows);
        Assert.False(metro.IsMultiple);
        Assert.Single(metro.DisplayedRows);
        Assert.Equal("Food", cafe.CategoryName);
        Assert.Equal(Banccoon.App.Formatting.MoneyFormat.Format(-(11m + 13m + 15m), "RUB"), cafe.TotalText);

        cafe.ToggleExpandedCommand.Execute(null);
        Assert.Equal([1, 3, 5], cafe.DisplayedRows.Select(r => r.Date.Day));

        sections.IsGroupedByName = false;
        sections.ToggleReadyExpandedCommand.Execute(null);
        Assert.True(sections.IsReadyListFlat);
        Assert.False(sections.IsReadyListGrouped);
        Assert.Equal(6, sections.ReadyRows.Count);
        Assert.Same(bakery, sections.ReadyGroups[0]);
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
        Assert.Empty(sections.ReadyGroups);
        review.Rows[0].Category = review.CategoryOptions.First(o => o.Name == "Food");

        review.Rows[0].ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        var shop = Assert.Single(sections.ReadyGroups);
        Assert.Equal("Shop", shop.Name);
        Assert.Equal([2, 3], shop.Rows.Select(r => r.Date.Day));
        Assert.Equal([2, 3], sections.ReadyRows.Select(r => r.Date.Day));
        Assert.Equal(4, Assert.Single(sections.AttentionRows).Date.Day);
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
