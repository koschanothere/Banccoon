using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Banccoon.Core.Transactions;
using Banccoon.Tests.Infrastructure;
using Xunit;

namespace Banccoon.Tests.Statements;

public sealed class StatementImportServiceTests
{
    [Fact]
    public async Task CreatePendingImportAsync_WhenNoParserAvailable_ReturnsUnsupportedResult()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        await store.Accounts.SaveAsync(account);
        var service = CreateService(store, Array.Empty<IStatementParser>());

        var result = await service.CreatePendingImportAsync(account.Id, "statement.unknown");

        Assert.False(result.ParserAvailable);
        Assert.Null(result.Batch);
        Assert.Equal(new StatementImportMessage(StatementImportMessageCode.NoParserAvailable), result.Message);
        Assert.Empty(await store.StatementImports.GetAllBatchesAsync());
    }

    [Fact]
    public async Task PreviewAsync_WhenNoFileChosen_ReturnsNoFileChosenMessage()
    {
        await using var store = new SqliteTestStore();
        var service = CreateService(store, Array.Empty<IStatementParser>());

        var result = await service.PreviewAsync("  ");

        Assert.False(result.ParserAvailable);
        Assert.Null(result.Statement);
        Assert.Equal(new StatementImportMessage(StatementImportMessageCode.NoFileChosen), result.Message);
    }

    [Fact]
    public async Task PreviewAsync_WhenNoParserAvailable_ReturnsNoParserMessage()
    {
        await using var store = new SqliteTestStore();
        var service = CreateService(store, Array.Empty<IStatementParser>());

        var result = await service.PreviewAsync("statement.unknown");

        Assert.False(result.ParserAvailable);
        Assert.Equal(new StatementImportMessage(StatementImportMessageCode.NoParserAvailable), result.Message);
    }

    [Fact]
    public async Task CreatePendingImportAsync_ReportsRowCountReadyForReview()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        await store.Accounts.SaveAsync(account);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(new DateOnly(2026, 6, 10), 25m, TransactionType.Expense, "Lunch"),
            new ParsedStatementRow(new DateOnly(2026, 6, 11), 40m, TransactionType.Expense, "Dinner")
        ])]);

        var result = await service.CreatePendingImportAsync(account.Id, "statement.fake");

        Assert.Equal(new StatementImportMessage(StatementImportMessageCode.RowsReadyForReview, 2), result.Message);
    }

    [Fact]
    public async Task CancelImportAsync_WhenBatchDoesNotExist_ReturnsImportNotFound()
    {
        await using var store = new SqliteTestStore();
        var service = CreateService(store, Array.Empty<IStatementParser>());

        var result = await service.CancelImportAsync(Guid.NewGuid());

        Assert.False(result.Cancelled);
        Assert.Equal(new StatementImportMessage(StatementImportMessageCode.ImportNotFound), result.Message);
    }

    [Fact]
    public async Task PreviewAsync_WhenParserAvailable_ReturnsParsedStatementBeforeAccountSelection()
    {
        await using var store = new SqliteTestStore();
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(
                new DateOnly(2026, 6, 10),
                25m,
                TransactionType.Expense,
                "Lunch")
        ])]);

        var result = await service.PreviewAsync("statement.fake");

        Assert.True(result.ParserAvailable);
        Assert.NotNull(result.Statement);
        Assert.Equal("fake", result.Statement.ParserId);
        Assert.Single(result.Statement.Rows);
        Assert.Equal(new StatementImportMessage(StatementImportMessageCode.RowsFound, 1), result.Message);
    }

    [Fact]
    public async Task ApproveRowAsync_CreatesTransactionAndUpdatesBalanceOnce()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        var category = new Category(Guid.NewGuid(), "Food", TransactionType.Expense);
        await store.Accounts.SaveAsync(account);
        await store.Categories.SaveAsync(category);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(
                new DateOnly(2026, 6, 10),
                25m,
                TransactionType.Expense,
                "Lunch",
                "Cafe")
        ])]);

        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");
        var row = Assert.Single(pending.Rows);

        var result = await service.ApproveRowAsync(row.Id, category.Id, type: null, destinationAccountId: null);

        var transactions = ForAccount(await store.Transactions.GetAllAsync(), account.Id);
        var updatedAccount = await store.Accounts.GetByIdAsync(account.Id);
        Assert.NotNull(result.Transaction);
        Assert.Equal(result.Transaction, Assert.Single(transactions));
        Assert.Equal(75m, updatedAccount?.CurrentBalance);
        Assert.Equal(StatementImportRowStatus.Approved, result.Row.Status);
    }

    [Fact]
    public async Task SkipRowAsync_DoesNotCreateTransaction()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        await store.Accounts.SaveAsync(account);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(
                new DateOnly(2026, 6, 10),
                25m,
                TransactionType.Expense,
                "Lunch")
        ])]);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");

        var skipped = await service.SkipRowAsync(Assert.Single(pending.Rows).Id);

        Assert.Equal(StatementImportRowStatus.Skipped, skipped.Status);
        Assert.Empty(ForAccount(await store.Transactions.GetAllAsync(), account.Id));
        Assert.Equal(100m, (await store.Accounts.GetByIdAsync(account.Id))?.CurrentBalance);
    }

    [Fact]
    public async Task CancelImportAsync_WhenNoRowsApproved_RemovesBatchAndRows()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        await store.Accounts.SaveAsync(account);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(
                new DateOnly(2026, 6, 10),
                25m,
                TransactionType.Expense,
                "Lunch")
        ])]);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");

        var result = await service.CancelImportAsync(pending.Batch!.Id);

        Assert.True(result.Cancelled);
        Assert.Equal(new StatementImportMessage(StatementImportMessageCode.Cancelled), result.Message);
        Assert.Empty(await store.StatementImports.GetAllBatchesAsync());
        Assert.Empty(await store.StatementImports.GetRowsByBatchIdAsync(pending.Batch.Id));
    }

    [Fact]
    public async Task CancelImportAsync_WhenRowsApproved_DoesNotRemoveBatch()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        await store.Accounts.SaveAsync(account);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(
                new DateOnly(2026, 6, 10),
                25m,
                TransactionType.Expense,
                "Lunch")
        ])]);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");
        await service.ApproveRowAsync(Assert.Single(pending.Rows).Id, null, type: null, destinationAccountId: null);

        var result = await service.CancelImportAsync(pending.Batch!.Id);

        Assert.False(result.Cancelled);
        Assert.Equal(new StatementImportMessage(StatementImportMessageCode.CannotCancelAfterApproval), result.Message);
        Assert.NotNull(await store.StatementImports.GetBatchByIdAsync(pending.Batch.Id));
    }

    [Fact]
    public async Task UndoReviewAsync_AfterApprove_DeletesTheTransactionAndRestoresTheBalance()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        var category = new Category(Guid.NewGuid(), "Food", TransactionType.Expense);
        await store.Accounts.SaveAsync(account);
        await store.Categories.SaveAsync(category);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(new DateOnly(2026, 6, 10), 25m, TransactionType.Expense, "Lunch"),
            new ParsedStatementRow(new DateOnly(2026, 6, 11), 5m, TransactionType.Expense, "Coffee")
        ])]);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");
        var lunch = pending.Rows.Single(row => row.Description == "Lunch");
        await service.ApproveRowAsync(lunch.Id, category.Id, type: null, destinationAccountId: null);

        var undone = await service.UndoReviewAsync(lunch.Id);

        Assert.Equal(StatementImportRowStatus.Pending, undone.Status);
        Assert.Null(undone.CreatedTransactionId);
        Assert.Equal(category.Id, undone.CategoryId);
        Assert.Empty(ForAccount(await store.Transactions.GetAllAsync(), account.Id));
        Assert.Equal(100m, (await store.Accounts.GetByIdAsync(account.Id))?.CurrentBalance);
        Assert.Equal(StatementImportRowStatus.Pending, (await store.StatementImports.GetRowByIdAsync(lunch.Id))?.Status);
    }

    [Fact]
    public async Task UndoReviewAsync_AfterTransferApprove_RestoresBothAccounts()
    {
        await using var store = new SqliteTestStore();
        var source = CreateAccount();
        var destination = CreateAccount() with { Id = Guid.NewGuid(), Name = "Savings", CurrentBalance = 10m };
        await store.Accounts.SaveAsync(source);
        await store.Accounts.SaveAsync(destination);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(new DateOnly(2026, 6, 10), 30m, TransactionType.Expense, "To savings"),
            new ParsedStatementRow(new DateOnly(2026, 6, 11), 5m, TransactionType.Expense, "Coffee")
        ])]);
        var pending = await service.CreatePendingImportAsync(source.Id, "statement.fake");
        var transferRow = pending.Rows.Single(row => row.Description == "To savings");
        await service.ApproveRowAsync(transferRow.Id, null, TransactionType.Transfer, destination.Id);

        await service.UndoReviewAsync(transferRow.Id);

        Assert.Equal(100m, (await store.Accounts.GetByIdAsync(source.Id))?.CurrentBalance);
        Assert.Equal(10m, (await store.Accounts.GetByIdAsync(destination.Id))?.CurrentBalance);
    }

    [Fact]
    public async Task UndoReviewAsync_AfterSkip_MakesTheRowPendingAgain()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        await store.Accounts.SaveAsync(account);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(new DateOnly(2026, 6, 10), 25m, TransactionType.Expense, "Lunch"),
            new ParsedStatementRow(new DateOnly(2026, 6, 11), 5m, TransactionType.Expense, "Coffee")
        ])]);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");
        await service.SkipRowAsync(pending.Rows[0].Id);

        var undone = await service.UndoReviewAsync(pending.Rows[0].Id);

        Assert.Equal(StatementImportRowStatus.Pending, undone.Status);
        Assert.Equal(100m, (await store.Accounts.GetByIdAsync(account.Id))?.CurrentBalance);
    }

    [Fact]
    public async Task UndoReviewAsync_OnceTheBatchIsComplete_IsRefused()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        await store.Accounts.SaveAsync(account);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(new DateOnly(2026, 6, 10), 25m, TransactionType.Expense, "Lunch")
        ], closingBalance: 75m)]);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");
        await service.ApproveRowAsync(pending.Rows[0].Id, null, type: null, destinationAccountId: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UndoReviewAsync(pending.Rows[0].Id));
        Assert.Single(ForAccount(await store.Transactions.GetAllAsync(), account.Id));
    }

    [Fact]
    public async Task CreatePendingImportAsync_FlagsLikelyDuplicates()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        var existingTransaction = new Transaction(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 10),
            25m,
            account.Id,
            null,
            "Statement import: Lunch | Reference: ref-1",
            TransactionType.Expense);
        await store.Accounts.SaveAsync(account);
        await store.Transactions.SaveAsync(existingTransaction);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(
                new DateOnly(2026, 6, 10),
                25m,
                TransactionType.Expense,
                "Lunch",
                ExternalReference: "ref-1")
        ])]);

        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");

        var row = Assert.Single(pending.Rows);
        Assert.True(row.IsDuplicate);
        Assert.Equal(existingTransaction.Id, row.DuplicateTransactionId);
    }

    [Fact]
    public async Task ApproveRowAsync_WithOutgoingTransfer_DebitsSourceAndCreditsDestination()
    {
        await using var store = new SqliteTestStore();
        var sourceAccount = CreateAccount();
        var destinationAccount = CreateAccount() with { Id = Guid.NewGuid(), CurrentBalance = 50m };
        await store.Accounts.SaveAsync(sourceAccount);
        await store.Accounts.SaveAsync(destinationAccount);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(
                new DateOnly(2026, 6, 10),
                25m,
                TransactionType.Expense,
                "Transfer out",
                "Some Recipient")
        ])]);
        var pending = await service.CreatePendingImportAsync(sourceAccount.Id, "statement.fake");
        var row = Assert.Single(pending.Rows);
        Assert.False(row.IsIncoming);

        var result = await service.ApproveRowAsync(row.Id, categoryId: null, type: TransactionType.Transfer, destinationAccountId: destinationAccount.Id);

        Assert.Equal(75m, (await store.Accounts.GetByIdAsync(sourceAccount.Id))?.CurrentBalance);
        Assert.Equal(75m, (await store.Accounts.GetByIdAsync(destinationAccount.Id))?.CurrentBalance);
        Assert.Equal(sourceAccount.Id, result.Transaction?.AccountId);
        Assert.Equal(destinationAccount.Id, result.Transaction?.DestinationAccountId);
    }

    [Fact]
    public async Task ApproveRowAsync_WithIncomingTransfer_ReversesSourceAndDestinationRoles()
    {
        // A row that arrived as money coming IN (the +/- sign on the original statement) must
        // credit the importing account and debit the other account - the opposite of the usual
        // "batch's own account is always the source" assumption - otherwise both balances move
        // in the wrong direction.
        await using var store = new SqliteTestStore();
        var importingAccount = CreateAccount();
        var otherAccount = CreateAccount() with { Id = Guid.NewGuid(), CurrentBalance = 200m };
        await store.Accounts.SaveAsync(importingAccount);
        await store.Accounts.SaveAsync(otherAccount);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(
                new DateOnly(2026, 6, 10),
                25m,
                TransactionType.Income,
                "Transfer in",
                "Some Recipient")
        ])]);
        var pending = await service.CreatePendingImportAsync(importingAccount.Id, "statement.fake");
        var row = Assert.Single(pending.Rows);
        Assert.True(row.IsIncoming);

        var result = await service.ApproveRowAsync(row.Id, categoryId: null, type: TransactionType.Transfer, destinationAccountId: otherAccount.Id);

        Assert.Equal(125m, (await store.Accounts.GetByIdAsync(importingAccount.Id))?.CurrentBalance);
        Assert.Equal(175m, (await store.Accounts.GetByIdAsync(otherAccount.Id))?.CurrentBalance);
        Assert.Equal(otherAccount.Id, result.Transaction?.AccountId);
        Assert.Equal(importingAccount.Id, result.Transaction?.DestinationAccountId);
    }

    [Fact]
    public async Task CreatePendingImportAsync_FlagsTransferAlreadyRecordedFromTheOtherAccountAsDuplicate()
    {
        // The two sides of a real transfer are worded completely differently by each bank's own
        // statement - date, close-enough time, and amount are the only reliable cross-statement
        // signal, so this deliberately uses unrelated description text on each side.
        await using var store = new SqliteTestStore();
        var sourceAccount = CreateAccount();
        var destinationAccount = CreateAccount() with { Id = Guid.NewGuid() };
        await store.Accounts.SaveAsync(sourceAccount);
        await store.Accounts.SaveAsync(destinationAccount);

        var sourceService = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(
                new DateOnly(2026, 6, 10),
                25m,
                TransactionType.Expense,
                "Transfer out",
                "Some Recipient",
                Time: new TimeOnly(10, 0))
        ])]);
        var sourcePending = await sourceService.CreatePendingImportAsync(sourceAccount.Id, "statement.fake");
        var sourceRow = Assert.Single(sourcePending.Rows);
        await sourceService.ApproveRowAsync(sourceRow.Id, categoryId: null, type: TransactionType.Transfer, destinationAccountId: destinationAccount.Id);

        var destinationService = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(
                new DateOnly(2026, 6, 10),
                25m,
                TransactionType.Income,
                "Transfer in",
                "Completely Different Wording",
                Time: new TimeOnly(10, 2))
        ])]);
        var destinationPending = await destinationService.CreatePendingImportAsync(destinationAccount.Id, "statement.fake");

        var destinationRow = Assert.Single(destinationPending.Rows);
        Assert.True(destinationRow.IsDuplicate);
    }

    [Fact]
    public async Task ApproveRowAsync_WhenBatchCompletes_OverridesBalanceWithStatementClosingBalance()
    {
        // The account started this import already 15 off from reality (a prior missed/duplicated
        // transaction, or just a wrong starting balance) - summing the one 25 expense from this
        // statement on top of that wrong number would land on 75, but the statement's own most
        // recent-operation balance says 60, and that should win once the batch is fully reviewed.
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        await store.Accounts.SaveAsync(account);
        var service = CreateService(store, [new FakeStatementParser(
            [
                new ParsedStatementRow(
                    new DateOnly(2026, 6, 10),
                    25m,
                    TransactionType.Expense,
                    "Lunch")
            ],
            closingBalance: 60m)]);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");
        var row = Assert.Single(pending.Rows);

        await service.ApproveRowAsync(row.Id, categoryId: null, type: null, destinationAccountId: null);

        Assert.Equal(60m, (await store.Accounts.GetByIdAsync(account.Id))?.CurrentBalance);
    }

    [Fact]
    public async Task SkipRowAsync_WhenBatchCompletes_StillOverridesBalanceWithStatementClosingBalance()
    {
        // Skipping a row means Banccoon never records that operation, but the real bank balance at
        // the end of the statement already accounts for it regardless - the override should still
        // apply once every row has been reviewed, skipped or not.
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        await store.Accounts.SaveAsync(account);
        var service = CreateService(store, [new FakeStatementParser(
            [
                new ParsedStatementRow(
                    new DateOnly(2026, 6, 10),
                    25m,
                    TransactionType.Expense,
                    "Lunch")
            ],
            closingBalance: 60m)]);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");

        await service.SkipRowAsync(Assert.Single(pending.Rows).Id);

        Assert.Equal(60m, (await store.Accounts.GetByIdAsync(account.Id))?.CurrentBalance);
    }

    [Fact]
    public async Task ApproveRowAsync_WhenBatchNotYetComplete_DoesNotOverrideBalance()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        await store.Accounts.SaveAsync(account);
        var service = CreateService(store, [new FakeStatementParser(
            [
                new ParsedStatementRow(new DateOnly(2026, 6, 10), 25m, TransactionType.Expense, "Lunch"),
                new ParsedStatementRow(new DateOnly(2026, 6, 11), 10m, TransactionType.Expense, "Coffee")
            ],
            closingBalance: 60m)]);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");
        var firstRow = pending.Rows[0];

        await service.ApproveRowAsync(firstRow.Id, categoryId: null, type: null, destinationAccountId: null);

        // Only one of two rows reviewed - the statement's closing balance shouldn't apply yet,
        // since it describes the state after ALL of the statement's operations, not just this one.
        Assert.Equal(75m, (await store.Accounts.GetByIdAsync(account.Id))?.CurrentBalance);
    }

    [Fact]
    public async Task GetPendingSuggestionsAsync_ReflectsRulesLearnedAfterTheImportWasCreated()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        var food = new Category(Guid.NewGuid(), "Food", TransactionType.Expense);
        await store.Accounts.SaveAsync(account);
        await store.Categories.SaveAsync(food);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(new DateOnly(2026, 6, 10), 5m, TransactionType.Expense, "Coffee", "Cafe"),
            new ParsedStatementRow(new DateOnly(2026, 6, 11), 6m, TransactionType.Expense, "Coffee", "Cafe"),
            new ParsedStatementRow(new DateOnly(2026, 6, 12), 3m, TransactionType.Expense, "Ticket", "Metro")
        ])]);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");
        Assert.All(pending.Rows, row => Assert.Null(row.SuggestedCategoryId));

        await service.ApproveRowAsync(pending.Rows[0].Id, food.Id, type: null, destinationAccountId: null);
        var suggestions = await service.GetPendingSuggestionsAsync(pending.Batch!.Id);

        Assert.Equal(2, suggestions.Count);
        var secondCafe = Assert.Single(suggestions, suggestion => suggestion.RowId == pending.Rows[1].Id);
        Assert.Equal(food.Id, secondCafe.CategoryId);
        Assert.Equal(TransactionType.Expense, secondCafe.Type);
        Assert.Null(Assert.Single(suggestions, suggestion => suggestion.RowId == pending.Rows[2].Id).CategoryId);
    }

    [Fact]
    public async Task GetPendingSuggestionsAsync_SuggestsALearnedTransferWithItsOtherAccount()
    {
        await using var store = new SqliteTestStore();
        var account = CreateAccount();
        var savings = CreateAccount() with { Id = Guid.NewGuid(), Name = "Savings", Type = AccountType.Savings };
        var moving = new Category(Guid.NewGuid(), "Moving money");
        await store.Accounts.SaveAsync(account);
        await store.Accounts.SaveAsync(savings);
        await store.Categories.SaveAsync(moving);
        var service = CreateService(store, [new FakeStatementParser([
            new ParsedStatementRow(new DateOnly(2026, 6, 10), 50m, TransactionType.Expense, "To savings", "Own account"),
            new ParsedStatementRow(new DateOnly(2026, 6, 20), 50m, TransactionType.Expense, "To savings", "Own account")
        ])]);
        var pending = await service.CreatePendingImportAsync(account.Id, "statement.fake");

        await service.ApproveRowAsync(pending.Rows[0].Id, moving.Id, TransactionType.Transfer, savings.Id);
        var suggestion = Assert.Single(await service.GetPendingSuggestionsAsync(pending.Batch!.Id));

        Assert.Equal(pending.Rows[1].Id, suggestion.RowId);
        Assert.Equal(TransactionType.Transfer, suggestion.Type);
        Assert.Equal(moving.Id, suggestion.CategoryId);
        Assert.Equal(savings.Id, suggestion.DestinationAccountId);
    }

    private static StatementImportService CreateService(
        SqliteTestStore store,
        IEnumerable<IStatementParser> parsers)
    {
        return new StatementImportService(
            new StatementParserRegistry(parsers),
            store.StatementImports,
            store.CategoryLearningRules,
            store.Accounts,
            store.Categories,
            store.Transactions,
            new TransactionApplicationService(new TransactionBalanceService()),
            new CategorySuggestionService(),
            store.BankCategoryLinks);
    }

    private static Account CreateAccount()
    {
        return new Account(
            Guid.NewGuid(),
            "Checking",
            AccountType.DebitCard,
            100m,
            "EUR",
            new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero));
    }

    private sealed class FakeStatementParser : IStatementParser
    {
        private readonly IReadOnlyList<ParsedStatementRow> rows;
        private readonly decimal? closingBalance;

        public FakeStatementParser(IReadOnlyList<ParsedStatementRow> rows, decimal? closingBalance = null)
        {
            this.rows = rows;
            this.closingBalance = closingBalance;
        }

        public StatementParserDescriptor Descriptor { get; } = new(
            "fake",
            "Fake statement parser",
            [".fake"]);

        public bool CanParse(StatementParseRequest request)
        {
            return request.FilePath.EndsWith(".fake", StringComparison.OrdinalIgnoreCase);
        }

        public Task<ParsedStatement> ParseAsync(
            StatementParseRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ParsedStatement(
                Descriptor.Id,
                Descriptor.Name,
                Path.GetFileName(request.FilePath),
                rows,
                ClosingBalance: closingBalance));
        }
    }

    private static IReadOnlyList<Transaction> ForAccount(IReadOnlyList<Transaction> transactions, Guid accountId) =>
        transactions.Where(transaction => transaction.AccountId == accountId || transaction.DestinationAccountId == accountId).ToList();
}
