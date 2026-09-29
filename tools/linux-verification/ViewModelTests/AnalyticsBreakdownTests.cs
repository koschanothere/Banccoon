using Banccoon.App.Localization;
using Banccoon.App.ViewModels;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Analytics;
using Banccoon.Core.Models;
using Banccoon.Tests.Infrastructure;
using Xunit;

// The Analytics card's donut rolls children up into their parent; clicking a parent's slice opens
// a second donut breaking that parent down.
public sealed class AnalyticsBreakdownTests
{
    [Fact]
    public async Task ParentSlice_RollsUpItsChildren_AndClickingItOpensAndClosesTheBreakdown()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var food = new Category(Guid.NewGuid(), "Food");
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        var cafes = new Category(Guid.NewGuid(), "Cafes", ParentCategoryId: food.Id);
        var transport = new Category(Guid.NewGuid(), "Transport");
        foreach (var category in new[] { food, groceries, cafes, transport })
        {
            await store.Categories.SaveAsync(category);
        }

        var card = new Account(Guid.NewGuid(), "Card", AccountType.DebitCard, 1000m, "RUB", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(card);
        var account = card.Id;
        foreach (var (amount, categoryId) in new[] { (10m, food.Id), (60m, groceries.Id), (30m, cafes.Id), (40m, transport.Id) })
        {
            await store.Transactions.SaveAsync(new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, 5), amount, account, categoryId, null, TransactionType.Expense));
        }

        var vm = new AnalyticsViewModel(new FixedDate(new DateOnly(2026, 6, 15)), store.Transactions, store.Categories, new AnalyticsService(), _ => Task.CompletedTask);
        await vm.InitializeAsync("RUB");

        Assert.Equal(["Food", "Transport"], vm.DonutSegments.Select(s => s.CategoryName));
        Assert.Equal(100m, vm.DonutSegments[0].Amount);
        Assert.True(vm.HasBreakdowns);
        Assert.False(vm.IsBreakdownOpen);

        // A parent with no children: nothing happens.
        vm.SelectDonutSegmentCommand.Execute(vm.DonutSegments[1]);
        Assert.False(vm.IsBreakdownOpen);
        Assert.Empty(vm.BreakdownSegments);

        vm.SelectDonutSegmentCommand.Execute(vm.DonutSegments[0]);
        Assert.True(vm.IsBreakdownOpen);
        Assert.Equal(["Groceries", "Cafes", "Food (no subcategory)"], vm.BreakdownSegments.Select(s => s.CategoryName));
        Assert.Equal(100m, vm.BreakdownSegments.Sum(s => s.Amount));
        Assert.Equal(3, vm.BreakdownSegments.Select(s => s.Color.ToArgbHex()).Distinct().Count());
        Assert.StartsWith("Food: ", vm.BreakdownCaptionText);

        vm.HighlightedBreakdownSegment = vm.BreakdownSegments[0];
        Assert.StartsWith("Groceries: ", vm.BreakdownCaptionText);
        Assert.Contains("60", vm.BreakdownCaptionText);

        vm.SelectDonutSegmentCommand.Execute(vm.DonutSegments[0]);
        Assert.False(vm.IsBreakdownOpen);
        Assert.Empty(vm.BreakdownSegments);
    }

    // Decided 2026-09-29: tapping a list row (Spending by category or Top movers) with a breakdown
    // opens/closes it like its slice does; its separate link, and any row without one, drill down.
    [Fact]
    public async Task RowWithBreakdown_TapTogglesIt_RowWithout_DrillsDown()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var food = new Category(Guid.NewGuid(), "Food");
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        var transport = new Category(Guid.NewGuid(), "Transport");
        foreach (var category in new[] { food, groceries, transport })
        {
            await store.Categories.SaveAsync(category);
        }

        var card = new Account(Guid.NewGuid(), "Card", AccountType.DebitCard, 1000m, "RUB", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(card);
        foreach (var (amount, categoryId) in new[] { (60m, groceries.Id), (40m, transport.Id) })
        {
            await store.Transactions.SaveAsync(new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, 5), amount, card.Id, categoryId, null, TransactionType.Expense));
        }

        var drilledInto = new List<Guid?>();
        var vm = new AnalyticsViewModel(new FixedDate(new DateOnly(2026, 6, 15)), store.Transactions, store.Categories, new AnalyticsService(), id =>
        {
            drilledInto.Add(id);
            return Task.CompletedTask;
        });
        await vm.InitializeAsync("RUB");

        var foodRow = vm.CategoryRows.Single(row => row.CategoryName == "Food");
        var transportRow = vm.CategoryRows.Single(row => row.CategoryName == "Transport");
        Assert.True(foodRow.OpensBreakdown);
        Assert.False(transportRow.OpensBreakdown);

        foodRow.TapCommand.Execute(null);
        Assert.True(vm.IsBreakdownOpen);
        Assert.Empty(drilledInto);

        // The top mover for Food is a separate row object; its tap closes the same breakdown.
        var foodMover = vm.TopMovers.Single(row => row.CategoryName == "Food");
        Assert.True(foodMover.OpensBreakdown);
        foodMover.TapCommand.Execute(null);
        Assert.False(vm.IsBreakdownOpen);

        foodRow.DrillDownCommand.Execute(null);
        transportRow.TapCommand.Execute(null);
        Assert.Equal([food.Id, transport.Id], drilledInto);
        Assert.False(vm.IsBreakdownOpen);
    }

    private sealed class FixedDate(DateOnly today) : IDateProvider
    {
        public DateOnly Today { get; } = today;
    }
}
