using Banccoon.App.Localization;
using Banccoon.App.ViewModels;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Banccoon.Core.Transactions;
using Banccoon.Tests.Infrastructure;
using Xunit;

public sealed class ReviewSectionsAndUndoTests
{
    [Fact]
    public async Task Rows_LandInDuplicateAttentionAndReadyBlocks()
    {
        await using var f = await Fixture.CreateAsync();
        var sections = f.Review.Sections;
        Assert.False(sections.IsReadyExpanded);
        Assert.Empty(sections.ReadyRows);
        Assert.Empty(sections.ReadyGroups);
        sections.ShowReadyRows();

        Assert.Equal(["Coffee House"], sections.ReadyRows.Select(r => r.Description));
        Assert.Equal(["Unknown shop", "Mystery"], sections.AttentionRows.Select(r => r.Description));
        Assert.Equal(["Already recorded"], sections.DuplicateRows.Select(r => r.Description));
        Assert.Equal(string.Format(Translator.Get("StatementImport_ReadyHeaderFormat"), 1), sections.ReadyHeaderText);
    }

    [Fact]
    public async Task RowsWithNothingLearned_StartWithNoCategory_InsteadOfTheFirstOneAlphabetically()
    {
        await using var f = await Fixture.CreateAsync();
        f.Review.Sections.ShowReadyRows();

        Assert.All(f.Review.Sections.AttentionRows, row => Assert.Null(row.Category));
        Assert.NotNull(f.Review.Sections.ReadyRows.Single().Category);
    }

    [Fact]
    public async Task ApproveAllCategorised_ApprovesReadyAndUserCategorisedRows_NeverDuplicatesOrUncategorised()
    {
        await using var f = await Fixture.CreateAsync();
        var review = f.Review;
        review.Sections.ShowReadyRows();
        var unknown = review.Sections.AttentionRows.Single(r => r.Description == "Unknown shop");
        unknown.Category = review.CategoryOptions.First(o => o.Name == "Fun");
        Assert.Equal(2, review.Sections.CategorisedCount);

        await f.RunAsync(review.Sections.ApproveAllCategorisedCommand);

        Assert.Equal(["Mystery"], review.Sections.AttentionRows.Select(r => r.Description));
        Assert.Single(review.Sections.DuplicateRows);
        Assert.Empty(review.Sections.ReadyRows);
        // Everything except the pre-existing "Already recorded" row and the May teaching import.
        var created = (await f.Store.Transactions.GetAllAsync()).Where(t => t.Date.Month == 6 && t.Name != "Already recorded").ToList();
        Assert.Equal(["Coffee House", "Unknown shop"], created.Select(t => t.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task SkipAllDuplicates_SkipsOnlyTheDuplicateBlock()
    {
        await using var f = await Fixture.CreateAsync();

        await f.RunAsync(f.Review.Sections.SkipAllDuplicatesCommand);

        Assert.Empty(f.Review.Sections.DuplicateRows);
        Assert.Equal(3, f.Review.Rows.Count);
    }

    [Fact]
    public async Task Progress_CountsReviewedRowsAgainstTheWholeStatement()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.Equal(string.Format(Translator.Get("StatementImport_ProgressFormat"), 0, 4), f.Review.ProgressText);

        await f.RunAsync(f.Review.Sections.SkipAllDuplicatesCommand);

        Assert.Equal(string.Format(Translator.Get("StatementImport_ProgressFormat"), 1, 4), f.Review.ProgressText);
    }

    [Fact]
    public async Task Undo_AfterApprove_RemovesTheTransactionRestoresBalanceAndPutsTheSameRowBack()
    {
        await using var f = await Fixture.CreateAsync();
        var review = f.Review;
        var balanceBefore = (await f.Store.Accounts.GetByIdAsync(f.AccountId))!.CurrentBalance;
        var mystery = review.Sections.AttentionRows.Single(r => r.Description == "Mystery");
        mystery.Category = review.CategoryOptions.First(o => o.Name == "Fun");
        mystery.Type = TransactionType.Income;

        await f.RunAsync(mystery.ApproveCommand);
        Assert.DoesNotContain(mystery, review.Rows);
        Assert.True(review.CanUndo);

        await f.RunAsync(review.UndoCommand);

        Assert.Contains(mystery, review.Sections.AttentionRows);
        Assert.Equal("Fun", mystery.Category?.Name);
        Assert.Equal(TransactionType.Income, mystery.Type);
        Assert.True(mystery.IsIdle);
        Assert.False(review.CanUndo);
        Assert.Equal(balanceBefore, (await f.Store.Accounts.GetByIdAsync(f.AccountId))!.CurrentBalance);
        Assert.DoesNotContain(await f.Store.Transactions.GetAllAsync(), t => t.Name == "Mystery");
        Assert.Equal(string.Format(Translator.Get("StatementImport_ProgressFormat"), 0, 4), review.ProgressText);
    }

    [Fact]
    public async Task Undo_AfterABulkAction_PutsEveryRowItReviewedBack()
    {
        await using var f = await Fixture.CreateAsync();
        var review = f.Review;
        review.Sections.ShowReadyRows();
        review.Sections.AttentionRows.Single(r => r.Description == "Unknown shop").Category = review.CategoryOptions.First(o => o.Name == "Fun");

        await f.RunAsync(review.Sections.ApproveAllCategorisedCommand);
        Assert.Equal(2, review.Rows.Count);
        await f.RunAsync(review.UndoCommand);

        Assert.Equal(4, review.Rows.Count);
        Assert.Single(review.Sections.ReadyRows);
        Assert.Equal(2, review.Sections.AttentionRows.Count);
        // Only the pre-existing row and the May teaching import remain.
        Assert.Equal(
            [("Already recorded", 6), ("Coffee House", 5)],
            (await f.Store.Transactions.GetAllAsync()).Select(t => (t.Name, t.Date.Month)).OrderBy(t => t.Name));
    }

    [Fact]
    public async Task Undo_IsNotOfferedOnceTheLastRowIsReviewed()
    {
        await using var f = await Fixture.CreateAsync();
        var review = f.Review;

        await f.RunAsync(review.Sections.SkipAllDuplicatesCommand);
        foreach (var row in review.Rows.ToList())
        {
            row.SkipCommand.Execute(null);
        }

        await f.SettleAsync();
        Assert.True(review.IsComplete);
        Assert.False(review.CanUndo);
    }

    [Fact]
    public async Task TypeEditor_OpensFromTheAmountAndClosesOnceATypeIsChosen()
    {
        await using var f = await Fixture.CreateAsync();
        var row = f.Review.Sections.AttentionRows[0];
        Assert.False(row.IsTypeEditorOpen);
        Assert.Equal(string.Format(Translator.Get("StatementImport_TypeTooltipFormat"), Translator.Get("Enum_TransactionType_Expense")), row.TypeToolTipText);

        row.ToggleTypeEditorCommand.Execute(null);
        Assert.True(row.IsTypeEditorOpen);
        row.Type = TransactionType.Transfer;

        Assert.False(row.IsTypeEditorOpen);
        Assert.False(row.IsReadyToApprove); // a transfer still needs its other account
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteTestStore Store { get; } = new();
        public StatementImportReviewViewModel Review { get; private set; } = null!;
        public Guid AccountId { get; } = Guid.NewGuid();

        public static async Task<Fixture> CreateAsync()
        {
            Translator.SetLanguage("en");
            var f = new Fixture();
            await f.Store.Accounts.SaveAsync(new Account(f.AccountId, "Card", AccountType.DebitCard, 1000m, "RUB", DateTimeOffset.UtcNow));
            await f.Store.Accounts.SaveAsync(new Account(Guid.NewGuid(), "Savings", AccountType.Savings, 0m, "RUB", DateTimeOffset.UtcNow));
            var food = new Category(Guid.NewGuid(), "Food", TransactionType.Expense);
            await f.Store.Categories.SaveAsync(food);
            await f.Store.Categories.SaveAsync(new Category(Guid.NewGuid(), "Fun", TransactionType.Expense));
            await f.Store.Transactions.SaveAsync(new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, 4), 99m, f.AccountId, null, null, TransactionType.Expense, Name: "Already recorded"));

            // Teach Banccoon "Coffee House" -> Food with an earlier, fully reviewed import.
            var teach = CreateService(f.Store, [new ParsedStatementRow(new DateOnly(2026, 5, 1), 3m, TransactionType.Expense, "Coffee House seed", "Coffee House")]);
            var taught = await teach.CreatePendingImportAsync(f.AccountId, "teach.fake");
            await teach.ApproveRowAsync(taught.Rows[0].Id, food.Id, type: null, destinationAccountId: null);

            var service = CreateService(f.Store,
            [
                new ParsedStatementRow(new DateOnly(2026, 6, 1), 4m, TransactionType.Expense, "Coffee House", "Coffee House"),
                new ParsedStatementRow(new DateOnly(2026, 6, 2), 15m, TransactionType.Expense, "Unknown shop"),
                new ParsedStatementRow(new DateOnly(2026, 6, 3), 40m, TransactionType.Expense, "Mystery"),
                new ParsedStatementRow(new DateOnly(2026, 6, 4), 99m, TransactionType.Expense, "Already recorded")
            ]);
            var pending = await service.CreatePendingImportAsync(f.AccountId, "statement.fake");
            f.Review = new StatementImportReviewViewModel(service, f.Store.StatementImports, f.Store.Categories, f.Store.Accounts);
            await f.Review.LoadAsync(pending.Batch!.Id, f.AccountId, "RUB");
            return f;
        }

        public async Task RunAsync(System.Windows.Input.ICommand command)
        {
            command.Execute(null);
            await SettleAsync();
        }

        public async Task SettleAsync()
        {
            for (var i = 0; i < 100; i++)
            {
                await Task.Delay(20);
                if (Review.Rows.All(row => row.IsIdle))
                {
                    await Task.Delay(80);
                    if (Review.Rows.All(row => row.IsIdle)) return;
                }
            }
        }

        public ValueTask DisposeAsync() => Store.DisposeAsync();

        private static StatementImportService CreateService(SqliteTestStore store, IReadOnlyList<ParsedStatementRow> rows)
        {
            return new StatementImportService(
                new StatementParserRegistry([new FakeParser(rows)]),
                store.StatementImports,
                store.CategoryLearningRules,
                store.Accounts,
                store.Categories,
                store.Transactions,
                new TransactionApplicationService(new TransactionBalanceService()),
                new CategorySuggestionService(),
                store.BankCategoryLinks);
        }

        private sealed class FakeParser(IReadOnlyList<ParsedStatementRow> rows) : IStatementParser
        {
            public StatementParserDescriptor Descriptor { get; } = new("fake", "Fake", [".fake"]);

            public bool CanParse(StatementParseRequest request) => request.FilePath.EndsWith(".fake", StringComparison.OrdinalIgnoreCase);

            public Task<ParsedStatement> ParseAsync(StatementParseRequest request, CancellationToken cancellationToken = default)
                => Task.FromResult(new ParsedStatement("fake", "Fake", request.FilePath, rows));
        }
    }
}
