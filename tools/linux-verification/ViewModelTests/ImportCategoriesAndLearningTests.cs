using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Banccoon.App.ViewModels;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Xunit;

// The 2026-09-25 fresh-install findings: a category created during review wasn't offered to the
// other rows and "+ New category" turned into a real category with no name box; and rules learned
// while approving didn't reach rows that were already loaded.
public sealed class ImportCategoriesAndLearningTests
{
    private static readonly ParsedStatementRow[] ThreeRecipients =
    [
        new(new DateOnly(2026, 6, 1), 10m, TransactionType.Expense, "Purchase", "Shop"),
        new(new DateOnly(2026, 6, 2), 10m, TransactionType.Expense, "Ticket", "Metro"),
        new(new DateOnly(2026, 6, 3), 10m, TransactionType.Expense, "Film", "Cinema")
    ];

    [Fact]
    public async Task CreatingACategory_OnAFreshInstall_KeepsTheOtherRowsNewCategoryBoxAndOffersItToEveryRow()
    {
        await using var fixture = await ReviewFixture.CreateAsync(ThreeRecipients, withCategories: false);
        var review = fixture.Review;
        var createNew = review.CategoryOptions.Single(o => o.IsCreateNew);
        var (first, second, third) = (review.Rows[0], review.Rows[1], review.Rows[2]);
        AttachPickers(review);
        first.Category = createNew;
        first.NewCategoryName = "Groceries";
        second.Category = createNew;
        second.NewCategoryName = "Transport";

        first.ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Equal(["Groceries"], review.CategoryOptions.Where(o => !o.IsCreateNew).Select(o => o.Name));
        Assert.True(review.CategoryOptions[^1].IsCreateNew);
        Assert.Same(createNew, second.Category);
        Assert.True(second.IsCreatingNewCategory);
        Assert.Equal("Transport", second.NewCategoryName);
        Assert.Null(third.Category);
    }

    [Fact]
    public async Task CreatingACategory_ThatSortsFirst_LeavesEveryOtherRowsPickAlone()
    {
        await using var fixture = await ReviewFixture.CreateAsync(ThreeRecipients);
        var review = fixture.Review;
        var fun = review.CategoryOptions.First(o => o.Name == "Fun");
        var createNew = review.CategoryOptions.Single(o => o.IsCreateNew);
        var (first, second) = (review.Rows[0], review.Rows[1]);
        AttachPickers(review);
        second.Category = fun;
        first.Category = createNew;
        first.NewCategoryName = "Drinks";

        first.ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Equal(["Drinks", "Food", "Fun"], review.CategoryOptions.Where(o => !o.IsCreateNew).Select(o => o.Name));
        Assert.Same(fun, second.Category);
    }

    [Fact]
    public async Task AddButton_CreatesTheCategoryAtOnce_AndPointsEveryRowTypingItAtIt()
    {
        await using var fixture = await ReviewFixture.CreateAsync(ThreeRecipients, withCategories: false);
        var review = fixture.Review;
        var createNew = review.CategoryOptions.Single(o => o.IsCreateNew);
        var (first, second, third) = (review.Rows[0], review.Rows[1], review.Rows[2]);
        AttachPickers(review);
        first.Category = createNew;
        first.NewCategoryName = "Coffee";
        second.Category = createNew;
        second.NewCategoryName = " coffee ";
        third.Category = createNew;
        third.NewCategoryName = "Tea";

        first.CreateCategoryCommand.Execute(null);
        await fixture.WaitForAsync(() => !first.IsCreatingNewCategory);

        var coffee = Assert.Single(await fixture.Store.Categories.GetAllAsync());
        Assert.Equal("Coffee", coffee.Name);
        Assert.Equal(coffee.Id, first.Category?.Id);
        Assert.Equal(string.Empty, first.NewCategoryName);
        Assert.Equal(coffee.Id, second.Category?.Id);
        Assert.Same(createNew, third.Category);
        Assert.Equal("Tea", third.NewCategoryName);
        Assert.Contains(review.CategoryOptions, o => o.Id == coffee.Id);
        Assert.Equal(3, review.Rows.Count);
        Assert.Empty(await fixture.Store.Transactions.GetAllAsync());

        first.ApproveCommand.Execute(null);
        await fixture.SettleAsync();
        Assert.Equal(["Coffee"], (await fixture.Store.Categories.GetAllAsync()).Select(c => c.Name));
    }

    [Fact]
    public async Task ApprovingWithNoCategory_OffersTheOtherCategoryItFiledTheRowUnder()
    {
        await using var fixture = await ReviewFixture.CreateAsync(ThreeRecipients, withCategories: false);
        var review = fixture.Review;

        review.Rows[0].ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        var other = Assert.Single(await fixture.Store.Categories.GetAllAsync());
        Assert.Contains(review.CategoryOptions, o => !o.IsCreateNew && o.Id == other.Id);
        Assert.True(review.CategoryOptions[^1].IsCreateNew);
    }

    [Fact]
    public async Task ApprovingARow_CategorisesUntouchedRowsFromTheSameRecipient_AndMovesThemToReady()
    {
        await using var fixture = await ReviewFixture.CreateAsync(
        [
            new(new DateOnly(2026, 6, 1), 4m, TransactionType.Expense, "Coffee", "Cafe"),
            new(new DateOnly(2026, 6, 2), 5m, TransactionType.Expense, "Coffee", "Cafe"),
            new(new DateOnly(2026, 6, 3), 6m, TransactionType.Expense, "Coffee", "Cafe"),
            new(new DateOnly(2026, 6, 4), 3m, TransactionType.Expense, "Ticket", "Metro")
        ]);
        var review = fixture.Review;
        var food = review.CategoryOptions.First(o => o.Name == "Food");
        var fun = review.CategoryOptions.First(o => o.Name == "Fun");
        var (first, second, third, metro) = (review.Rows[0], review.Rows[1], review.Rows[2], review.Rows[3]);
        Assert.Equal(4, review.Sections.AttentionRows.Count);
        third.Category = fun;
        first.Category = food;

        first.ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Same(food, second.Category);
        Assert.Contains(second, review.Sections.ReadyRows);
        Assert.DoesNotContain(second, review.Sections.AttentionRows);
        // Picked by hand - not overwritten, and doesn't jump blocks while being edited.
        Assert.Same(fun, third.Category);
        Assert.Contains(third, review.Sections.AttentionRows);
        Assert.Null(metro.Category);
        Assert.Contains(metro, review.Sections.AttentionRows);
        Assert.Equal(2, review.Sections.CategorisedCount);
    }

    [Fact]
    public async Task ApprovingATransfer_TeachesUntouchedRowsItsTypeAndOtherAccount()
    {
        await using var fixture = await ReviewFixture.CreateAsync(
        [
            new(new DateOnly(2026, 6, 1), 50m, TransactionType.Expense, "To savings", "Own account"),
            new(new DateOnly(2026, 6, 15), 50m, TransactionType.Expense, "To savings", "Own account")
        ]);
        var review = fixture.Review;
        var food = review.CategoryOptions.First(o => o.Name == "Food");
        var savings = Assert.Single(review.OtherAccountOptions);
        var (first, second) = (review.Rows[0], review.Rows[1]);
        first.Type = TransactionType.Transfer;
        first.OtherAccount = savings;
        first.Category = food;

        first.ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Equal(TransactionType.Transfer, second.Type);
        Assert.Same(savings, second.OtherAccount);
        Assert.Same(food, second.Category);
        Assert.Contains(second, review.Sections.ReadyRows);
    }

    private static void AttachPickers(StatementImportReviewViewModel review)
    {
        foreach (var row in review.Rows)
        {
            _ = new PickerSimulator(review.CategoryOptions, row);
        }
    }

    // Stands in for the MAUI Picker in a review row: ItemsSource is the shared option list and
    // SelectedItem is two-way bound to row.Category. Reproduces what MAUI 10's Picker does when an
    // item is inserted into ItemsSource (src/Controls/src/Core/Picker/Picker.cs, AddItems): an insert
    // at or before the selected index re-reads the item at the OLD index and writes it back through
    // the binding - the cause of "+ New category" turning into the category just created.
    private sealed class PickerSimulator
    {
        private int selectedIndex;

        public PickerSimulator(ObservableCollection<CategoryOptionViewModel> options, StatementImportRowViewModel row)
        {
            selectedIndex = row.Category is null ? -1 : options.IndexOf(row.Category);
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(StatementImportRowViewModel.Category))
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
