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
        Assert.Empty(await store.StatementImports.GetAllBatchesAsync());
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

        var transactions = await store.Transactions.GetByAccountIdAsync(account.Id);
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
        Assert.Empty(await store.Transactions.GetByAccountIdAsync(account.Id));
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
        Assert.NotNull(await store.StatementImports.GetBatchByIdAsync(pending.Batch.Id));
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
            new CategorySuggestionService());
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

        public FakeStatementParser(IReadOnlyList<ParsedStatementRow> rows)
        {
            this.rows = rows;
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
                rows));
        }
    }
}
