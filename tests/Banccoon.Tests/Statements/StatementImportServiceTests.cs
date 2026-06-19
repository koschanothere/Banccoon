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

        var result = await service.ApproveRowAsync(row.Id, category.Id);

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
        await service.ApproveRowAsync(Assert.Single(pending.Rows).Id, null);

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
            new TransactionBalanceService(),
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
