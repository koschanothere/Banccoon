using Banccoon.App.Localization;
using Banccoon.App.ViewModels;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Banccoon.Tests.Infrastructure;
using Xunit;

// Two-level categories in the pickers: every picker lists parents only, and the chosen parent's
// children appear in a second, optional picker (SubcategoryPickerViewModel).
public sealed class CategoryHierarchyPickerTests
{
    [Fact]
    public async Task AddForm_ListsParentsOnly_AndSavesTheChosenChild()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var (food, groceries) = await SeedAsync(store);
        await store.Categories.SaveAsync(new Category(Guid.NewGuid(), "Transport"));
        await store.Accounts.SaveAsync(new Account(Guid.NewGuid(), "Card", AccountType.DebitCard, 1000m, "RUB", DateTimeOffset.UtcNow));
        var vm = TransactionsDeleteTests.CreateViewModel(store);
        await vm.InitializeAsync();

        await vm.AddForm.OpenAsync(TransactionType.Expense);
        Assert.Equal(["Food", "Transport"], vm.AddForm.CategoryOptions.Where(o => o.IsCategory).Select(o => o.Name));

        vm.AddForm.Category = vm.AddForm.CategoryOptions.Single(o => o.Name == "Transport");
        Assert.False(vm.AddForm.Subcategory.IsVisible);

        vm.AddForm.Category = vm.AddForm.CategoryOptions.Single(o => o.Name == "Food");
        Assert.True(vm.AddForm.Subcategory.IsVisible);
        Assert.Equal(["No subcategory", "Groceries"], vm.AddForm.Subcategory.Options.Select(o => o.Name));
        Assert.True(vm.AddForm.Subcategory.Selected!.IsNone);

        vm.AddForm.Subcategory.Selected = vm.AddForm.Subcategory.Options.Single(o => o.Name == "Groceries");
        vm.AddForm.Name = "Shop";
        vm.AddForm.AmountText = "12";
        vm.AddForm.SaveCommand.Execute(null);
        await Task.Delay(300);

        var saved = Assert.Single(await store.Transactions.GetAllAsync());
        Assert.Equal(groceries.Id, saved.CategoryId);

        // A null from the picker while it redraws is not a choice (see SubcategoryPickerViewModel).
        await vm.AddForm.OpenAsync(TransactionType.Expense);
        vm.AddForm.Category = vm.AddForm.CategoryOptions.Single(o => o.Name == "Food");
        vm.AddForm.Subcategory.Selected = vm.AddForm.Subcategory.Options.Single(o => o.Name == "Groceries");
        vm.AddForm.Subcategory.Selected = null;
        Assert.Equal(groceries.Id, vm.AddForm.Subcategory.ChildId);
        _ = food;
    }

    [Fact]
    public async Task TransactionsFilter_ParentCoversItsChildren_AndTheSubcategoryNarrowsIt()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var (food, groceries) = await SeedAsync(store);
        var card = new Account(Guid.NewGuid(), "Card", AccountType.DebitCard, 1000m, "RUB", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(card);
        await TransactionsDeleteTests.RecordAsync(store, new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, 10), 5m, card.Id, food.Id, null, TransactionType.Expense, Name: "Snack"));
        await TransactionsDeleteTests.RecordAsync(store, new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, 11), 50m, card.Id, groceries.Id, null, TransactionType.Expense, Name: "Market"));
        await TransactionsDeleteTests.RecordAsync(store, new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, 12), 9m, card.Id, null, null, TransactionType.Expense, Name: "Other"));
        var vm = TransactionsDeleteTests.CreateViewModel(store);
        await vm.InitializeAsync();

        Assert.DoesNotContain(vm.CategoryOptions, o => o.Name == "Groceries");
        vm.CategoryFilter = vm.CategoryOptions.Single(o => o.Name == "Food");
        Assert.Equal(["Market", "Snack"], vm.Rows.Select(r => r.Name).OrderBy(n => n));
        Assert.Equal("All of the category", vm.CategoryFilterSubcategory.Options[0].Name);

        vm.CategoryFilterSubcategory.Selected = vm.CategoryFilterSubcategory.Options.Single(o => o.Name == "Groceries");
        Assert.Equal(["Market"], vm.Rows.Select(r => r.Name));

        // A reload (e.g. after a bulk edit) keeps parent + child.
        await vm.InitializeAsync();
        Assert.Equal("Food", vm.CategoryFilter!.Name);
        Assert.Equal(groceries.Id, vm.CategoryFilterSubcategory.ChildId);
        Assert.Equal(["Market"], vm.Rows.Select(r => r.Name));

        // Bulk: set the uncategorised one to the child.
        vm.CategoryFilter = vm.CategoryOptions[0];
        vm.ToggleSelectModeCommand.Execute(null);
        vm.Rows.Single(r => r.Name == "Other").IsSelected = true;
        vm.BulkCategory = vm.BulkCategoryOptions.Single(o => o.Name == "Food");
        vm.BulkSubcategory.Selected = vm.BulkSubcategory.Options.Single(o => o.Name == "Groceries");
        vm.AssignCategoryToSelectedCommand.Execute(null);
        await Task.Delay(300);
        Assert.Equal(groceries.Id, (await store.Transactions.GetAllAsync()).Single(t => t.Name == "Other").CategoryId);
    }

    [Fact]
    public async Task ImportRow_ApprovedUnderAChild_TeachesTheOtherRowsTheChildAndShowsItAsParentPlusChild()
    {
        Category? groceries = null;
        await using var fixture = await ReviewFixture.CreateAsync(
            Enumerable.Range(1, 3).Select(i => new ParsedStatementRow(new DateOnly(2026, 6, i), 10m, TransactionType.Expense, $"Row {i}")).ToArray(),
            seed: async (store, _) =>
            {
                var food = (await store.Categories.GetAllAsync()).Single(c => c.Name == "Food");
                groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
                await store.Categories.SaveAsync(groceries);
            });
        var review = fixture.Review;
        Assert.DoesNotContain(review.CategoryOptions, o => o.Name == "Groceries");

        var first = review.Rows[0];
        first.Category = review.CategoryOptions.Single(o => o.Name == "Food");
        Assert.True(first.Subcategory.IsVisible);
        first.Subcategory.Selected = first.Subcategory.Options.Single(o => o.Name == "Groceries");
        Assert.Equal(groceries!.Id, first.CategoryId);

        first.ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        var transaction = Assert.Single(await fixture.Store.Transactions.GetAllAsync());
        Assert.Equal(groceries.Id, transaction.CategoryId);
        Assert.All(review.Rows, row =>
        {
            Assert.Equal("Food", row.Category!.Name);
            Assert.Equal(groceries.Id, row.Subcategory.ChildId);
            Assert.Equal(groceries.Id, row.CategoryId);
            Assert.Equal(StatementImportRowSection.Ready, row.Section);
        });
    }

    [Fact]
    public async Task ImportBulk_AppliesTheChosenChildToTheSelectedRows()
    {
        Category? groceries = null;
        await using var fixture = await ReviewFixture.CreateAsync(
            [new ParsedStatementRow(new DateOnly(2026, 6, 1), 10m, TransactionType.Expense, "Alpha"),
             new ParsedStatementRow(new DateOnly(2026, 6, 2), 10m, TransactionType.Expense, "Beta")],
            seed: async (store, _) =>
            {
                var food = (await store.Categories.GetAllAsync()).Single(c => c.Name == "Food");
                groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
                await store.Categories.SaveAsync(groceries);
            });
        var review = fixture.Review;
        review.Bulk.ToggleSelectModeCommand.Execute(null);
        foreach (var row in review.Rows)
        {
            row.IsSelected = true;
        }

        review.Bulk.BulkCategory = review.CategoryOptions.Single(o => o.Name == "Food");
        review.Bulk.Subcategory.Selected = review.Bulk.Subcategory.Options.Single(o => o.Name == "Groceries");
        review.Bulk.ApplyCategoryCommand.Execute(null);
        await fixture.WaitForAsync(() => review.Rows.All(r => r.CategoryId == groceries!.Id));

        Assert.All(review.Rows, row => Assert.Equal("Food", row.Category!.Name));
    }

    private static async Task<(Category Food, Category Groceries)> SeedAsync(SqliteTestStore store)
    {
        var food = new Category(Guid.NewGuid(), "Food");
        var groceries = new Category(Guid.NewGuid(), "Groceries", ParentCategoryId: food.Id);
        await store.Categories.SaveAsync(food);
        await store.Categories.SaveAsync(groceries);
        return (food, groceries);
    }
}
