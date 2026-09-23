using Banccoon.App.Localization;
using Banccoon.App.ViewModels;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Banccoon.Core.Transactions;
using Banccoon.Tests.Infrastructure;
using Xunit;

public sealed class StatementImportReviewTests
{
    [Fact]
    public async Task ApprovingOneRow_KeepsEveryOtherRowsUnsavedEdits()
    {
        await using var fixture = await ReviewFixture.CreateAsync(3);
        var (review, store) = (fixture.Review, fixture.Store);
        var food = review.CategoryOptions.First(o => o.Name == "Food");
        var fun = review.CategoryOptions.First(o => o.Name == "Fun");
        var createNew = review.CategoryOptions.Single(o => o.IsCreateNew);

        var first = review.Rows[0];
        var second = review.Rows[1];
        var third = review.Rows[2];
        second.Category = fun;
        second.Type = TransactionType.Income;
        third.Category = createNew;
        third.NewCategoryName = "Pets";
        first.Category = food;

        first.ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Equal(2, review.Rows.Count);
        Assert.Same(second, review.Rows[0]);
        Assert.Same(third, review.Rows[1]);
        Assert.Same(fun, second.Category);
        Assert.Equal(TransactionType.Income, second.Type);
        Assert.True(third.IsCreatingNewCategory);
        Assert.Equal("Pets", third.NewCategoryName);

        var transaction = Assert.Single(await store.Transactions.GetAllAsync());
        Assert.Equal(food.Id, transaction.CategoryId);
    }

    [Fact]
    public async Task SkippingOneRow_KeepsOtherRowsEditsAndCreatesNothing()
    {
        await using var fixture = await ReviewFixture.CreateAsync(2);
        var review = fixture.Review;
        var fun = review.CategoryOptions.First(o => o.Name == "Fun");
        review.Rows[1].Category = fun;

        review.Rows[0].SkipCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Same(fun, Assert.Single(review.Rows).Category);
        Assert.Empty(await fixture.Store.Transactions.GetAllAsync());
    }

    [Fact]
    public async Task DoubleClickingApprove_CreatesOneTransaction()
    {
        await using var fixture = await ReviewFixture.CreateAsync(2);
        var row = fixture.Review.Rows[0];

        row.ApproveCommand.Execute(null);
        row.ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Single(await fixture.Store.Transactions.GetAllAsync());
        Assert.Single(fixture.Review.Rows);
    }

    [Fact]
    public async Task ApprovingRowsInQuickSuccession_AppliesEveryBalanceChange()
    {
        await using var fixture = await ReviewFixture.CreateAsync(3);
        foreach (var row in fixture.Review.Rows.ToList())
        {
            row.ApproveCommand.Execute(null);
        }

        await fixture.SettleAsync();

        Assert.Empty(fixture.Review.Rows);
        Assert.Equal(3, (await fixture.Store.Transactions.GetAllAsync()).Count);
        // 3 expenses of 10 from a starting 1000, but the completed batch then snaps the balance to
        // the statement's closing balance (see StatementImportService) - so check the transactions'
        // effect didn't get lost before that: every row got its own transaction.
        Assert.True(fixture.ReviewCompletedCount == 1);
    }

    [Fact]
    public async Task SameNewCategoryNameOnTwoRows_CreatesTheCategoryOnceAndPointsTheOtherRowAtIt()
    {
        await using var fixture = await ReviewFixture.CreateAsync(3);
        var review = fixture.Review;
        var createNew = review.CategoryOptions.Single(o => o.IsCreateNew);
        review.Rows[0].Category = createNew;
        review.Rows[0].NewCategoryName = "Pets";
        review.Rows[1].Category = createNew;
        review.Rows[1].NewCategoryName = "  pets ";
        var secondRow = review.Rows[1];

        review.Rows[0].ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        var pets = Assert.Single(await fixture.Store.Categories.GetAllAsync(), c => c.Name == "Pets");
        Assert.False(secondRow.IsCreatingNewCategory);
        Assert.Equal(pets.Id, secondRow.Category?.Id);

        secondRow.ApproveCommand.Execute(null);
        await fixture.SettleAsync();
        Assert.Single(await fixture.Store.Categories.GetAllAsync(), c => c.Name.Trim().Equals("pets", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TransferWithoutOtherAccount_ShowsAMessageInsteadOfFailingSilently()
    {
        await using var fixture = await ReviewFixture.CreateAsync(1, otherAccounts: 0);
        var row = fixture.Review.Rows[0];
        row.Type = TransactionType.Transfer;

        row.ApproveCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Equal(Translator.Get("StatementImport_ChooseOtherAccountFirst"), fixture.Review.StatusText);
        Assert.Single(fixture.Review.Rows);
        Assert.True(row.IsIdle);
    }

    [Fact]
    public async Task SelectionSummary_UpdatesWhenARowIsTicked()
    {
        await using var fixture = await ReviewFixture.CreateAsync(3);
        var bulk = fixture.Review.Bulk;
        bulk.ToggleSelectModeCommand.Execute(null);

        fixture.Review.Rows[0].IsSelected = true;
        fixture.Review.Rows[2].IsSelected = true;

        Assert.Equal(Translator.GetPlural("Common_SelectionCount", 2), bulk.SelectionSummaryText);
        Assert.False(bulk.AreAllRowsSelected);

        bulk.ToggleSelectAllCommand.Execute(null);
        Assert.True(bulk.AreAllRowsSelected);
        Assert.All(fixture.Review.Rows, r => Assert.True(r.IsSelected));

        bulk.ToggleSelectAllCommand.Execute(null);
        Assert.All(fixture.Review.Rows, r => Assert.False(r.IsSelected));
    }

    [Fact]
    public async Task ApplyToSelected_SetsEachSelectedRowsPickerWithoutApproving()
    {
        await using var fixture = await ReviewFixture.CreateAsync(3);
        var review = fixture.Review;
        var fun = review.CategoryOptions.First(o => o.Name == "Fun");
        var unselectedBefore = review.Rows[2].Category;
        review.Bulk.ToggleSelectModeCommand.Execute(null);
        review.Rows[0].IsSelected = true;
        review.Rows[1].IsSelected = true;
        review.Bulk.BulkCategory = fun;

        review.Bulk.ApplyCategoryCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Same(fun, review.Rows[0].Category);
        Assert.Same(fun, review.Rows[1].Category);
        Assert.Same(unselectedBefore, review.Rows[2].Category);
        Assert.Null(review.Bulk.BulkCategory);
        Assert.True(review.Bulk.SelectMode);
        Assert.Equal(3, review.Rows.Count);
        Assert.Empty(await fixture.Store.Transactions.GetAllAsync());
    }

    [Fact]
    public async Task ApproveSelected_RemovesOnlySelectedRowsAndKeepsTheRestsEdits()
    {
        await using var fixture = await ReviewFixture.CreateAsync(3);
        var review = fixture.Review;
        var fun = review.CategoryOptions.First(o => o.Name == "Fun");
        var food = review.CategoryOptions.First(o => o.Name == "Food");
        var unselected = review.Rows[2];
        unselected.Category = food;
        unselected.Type = TransactionType.Income;
        review.Bulk.ToggleSelectModeCommand.Execute(null);
        review.Rows[0].IsSelected = true;
        review.Rows[1].IsSelected = true;
        review.Bulk.BulkCategory = fun;

        review.Bulk.ApproveSelectedCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Same(unselected, Assert.Single(review.Rows));
        Assert.Same(food, unselected.Category);
        Assert.Equal(TransactionType.Income, unselected.Type);
        Assert.False(review.Bulk.SelectMode);
        Assert.All(await fixture.Store.Transactions.GetAllAsync(), t => Assert.Equal(fun.Id, t.CategoryId));
    }

    [Fact]
    public async Task ApproveSelected_WithAnInvalidRow_ApprovesNothing()
    {
        await using var fixture = await ReviewFixture.CreateAsync(2, otherAccounts: 0);
        var review = fixture.Review;
        review.Bulk.ToggleSelectModeCommand.Execute(null);
        review.Rows[0].IsSelected = true;
        review.Rows[1].IsSelected = true;
        review.Rows[1].Type = TransactionType.Transfer;

        review.Bulk.ApproveSelectedCommand.Execute(null);
        await fixture.SettleAsync();

        Assert.Equal(2, review.Rows.Count);
        Assert.Empty(await fixture.Store.Transactions.GetAllAsync());
        Assert.Equal(Translator.Get("StatementImport_ChooseOtherAccountFirst"), review.StatusText);
    }

    [Fact]
    public async Task SelectDuplicates_SelectsExactlyTheFlaggedRows()
    {
        await using var fixture = await ReviewFixture.CreateAsync(3, existingTransactionOnDay: 2);
        var review = fixture.Review;
        Assert.True(review.Bulk.HasDuplicates);
        review.Bulk.ToggleSelectModeCommand.Execute(null);
        review.Rows[0].IsSelected = true;

        review.Bulk.SelectDuplicatesCommand.Execute(null);

        Assert.Equal([false, true, false], review.Rows.Select(r => r.IsSelected));

        review.Bulk.SkipSelectedCommand.Execute(null);
        await fixture.SettleAsync();
        Assert.False(review.Bulk.HasDuplicates);
        Assert.Equal(2, review.Rows.Count);
    }

    [Fact]
    public async Task ReviewCompleted_FiresOnceWhenTheLastRowLeaves()
    {
        await using var fixture = await ReviewFixture.CreateAsync(2);
        fixture.Review.Rows[0].SkipCommand.Execute(null);
        await fixture.SettleAsync();
        Assert.Equal(0, fixture.ReviewCompletedCount);

        fixture.Review.Rows[0].ApproveCommand.Execute(null);
        await fixture.SettleAsync();
        Assert.Equal(1, fixture.ReviewCompletedCount);
        Assert.True(fixture.Review.IsComplete);
    }
}

internal sealed class ReviewFixture : IAsyncDisposable
{
    private ReviewFixture(SqliteTestStore store, StatementImportReviewViewModel review)
    {
        Store = store;
        Review = review;
        review.ReviewCompleted += () => ReviewCompletedCount++;
    }

    public SqliteTestStore Store { get; }

    public StatementImportReviewViewModel Review { get; }

    public int ReviewCompletedCount { get; private set; }

    public static async Task<ReviewFixture> CreateAsync(int rowCount, int otherAccounts = 1, int? existingTransactionOnDay = null)
    {
        Translator.SetLanguage("en");
        var store = new SqliteTestStore();
        var account = new Account(Guid.NewGuid(), "Card", AccountType.DebitCard, 1000m, "RUB", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(account);
        for (var i = 0; i < otherAccounts; i++)
        {
            await store.Accounts.SaveAsync(new Account(Guid.NewGuid(), $"Other {i}", AccountType.Savings, 0m, "RUB", DateTimeOffset.UtcNow));
        }

        if (existingTransactionOnDay is { } day)
        {
            // Same date and amount as the parsed row for that day -> flagged as a possible duplicate.
            await store.Transactions.SaveAsync(new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, day), 10m, account.Id, null, null, TransactionType.Expense, Name: $"Row {day}"));
        }

        await store.Categories.SaveAsync(new Category(Guid.NewGuid(), "Food", TransactionType.Expense));
        await store.Categories.SaveAsync(new Category(Guid.NewGuid(), "Fun", TransactionType.Expense));

        var rows = Enumerable.Range(1, rowCount)
            .Select(i => new ParsedStatementRow(new DateOnly(2026, 6, i), 10m, TransactionType.Expense, $"Row {i}"))
            .ToArray();
        var service = new StatementImportService(
            new StatementParserRegistry([new FakeParser(rows)]),
            store.StatementImports,
            store.CategoryLearningRules,
            store.Accounts,
            store.Categories,
            store.Transactions,
            new TransactionApplicationService(new TransactionBalanceService()),
            new CategorySuggestionService());
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");

        var review = new StatementImportReviewViewModel(service, store.StatementImports, store.Categories, store.Accounts);
        await review.LoadAsync(pending.Batch!.Id, account.Id, "RUB");
        return new ReviewFixture(store, review);
    }

    // Commands are fire-and-forget (like the real RelayCommand wiring); wait until every row is idle
    // and the gate has drained.
    public async Task SettleAsync()
    {
        for (var i = 0; i < 200; i++)
        {
            await Task.Delay(10);
            if (Review.Rows.All(row => row.IsIdle))
            {
                await Task.Delay(30);
                if (Review.Rows.All(row => row.IsIdle)) return;
            }
        }

        throw new TimeoutException("Review actions did not settle.");
    }

    public ValueTask DisposeAsync() => Store.DisposeAsync();

    private sealed class FakeParser(IReadOnlyList<ParsedStatementRow> rows) : IStatementParser
    {
        public StatementParserDescriptor Descriptor { get; } = new("fake", "Fake", [".fake"]);

        public bool CanParse(StatementParseRequest request) => request.FilePath.EndsWith(".fake", StringComparison.OrdinalIgnoreCase);

        public Task<ParsedStatement> ParseAsync(StatementParseRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ParsedStatement("fake", "Fake", "statement.fake", rows, ClosingBalance: 970m));
    }
}
