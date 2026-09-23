using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Banccoon.Core.Transactions;
using Banccoon.Tests.Infrastructure;
using Xunit;

namespace Banccoon.Tests.Transactions;

public sealed class TransactionDeletionServiceTests
{
    [Theory]
    [InlineData(TransactionType.Expense, 1000)]
    [InlineData(TransactionType.Income, 1000)]
    public async Task DeleteAsync_ReversesTheTransactionsEffectOnItsAccount(TransactionType type, decimal expectedBalance)
    {
        await using var store = new SqliteTestStore();
        var account = await SaveAccountAsync(store, "Card", 1000m);
        var transaction = await RecordAsync(store, account, 40m, type);

        var deleted = await CreateService(store).DeleteAsync([transaction.Id]);

        Assert.Equal(1, deleted);
        Assert.Empty(await store.Transactions.GetAllAsync());
        Assert.Equal(expectedBalance, (await store.Accounts.GetByIdAsync(account.Id))!.CurrentBalance);
    }

    [Fact]
    public async Task DeleteAsync_Transfer_RestoresBothAccounts()
    {
        await using var store = new SqliteTestStore();
        var source = await SaveAccountAsync(store, "Card", 100m);
        var destination = await SaveAccountAsync(store, "Savings", 20m);
        var transfer = await RecordAsync(store, source, 30m, TransactionType.Transfer, destination);

        await CreateService(store).DeleteAsync([transfer.Id]);

        Assert.Equal(100m, (await store.Accounts.GetByIdAsync(source.Id))!.CurrentBalance);
        Assert.Equal(20m, (await store.Accounts.GetByIdAsync(destination.Id))!.CurrentBalance);
    }

    [Fact]
    public async Task DeleteAsync_SeveralOnTheSameAccount_ReversesEachOne()
    {
        await using var store = new SqliteTestStore();
        var account = await SaveAccountAsync(store, "Card", 500m);
        var lunch = await RecordAsync(store, account, 25m, TransactionType.Expense);
        var salary = await RecordAsync(store, account, 300m, TransactionType.Income);
        var coffee = await RecordAsync(store, account, 5m, TransactionType.Expense);
        var kept = await RecordAsync(store, account, 70m, TransactionType.Expense);

        var deleted = await CreateService(store).DeleteAsync([lunch.Id, salary.Id, coffee.Id, Guid.NewGuid()]);

        Assert.Equal(3, deleted);
        Assert.Equal(kept, Assert.Single(await store.Transactions.GetAllAsync()));
        Assert.Equal(430m, (await store.Accounts.GetByIdAsync(account.Id))!.CurrentBalance);
    }

    [Fact]
    public async Task DeleteAsync_OfAnImportedTransaction_ClearsTheImportRowsLinkInsteadOfBreakingIt()
    {
        await using var store = new SqliteTestStore();
        var account = await SaveAccountAsync(store, "Card", 1000m);
        var transaction = await RecordAsync(store, account, 40m, TransactionType.Expense);
        var batch = new StatementImportBatch(Guid.NewGuid(), account.Id, "fake", "Fake", "s.pdf", null, DateTimeOffset.UtcNow, StatementImportBatchStatus.Completed, 1);
        await store.StatementImports.SaveBatchAsync(batch);
        var row = new StatementImportRow(Guid.NewGuid(), batch.Id, transaction.Date, 40m, TransactionType.Expense, "Lunch", "lunch", null, null, null, null, null, StatementImportRowStatus.Approved, false, null, transaction.Id);
        await store.StatementImports.SaveRowAsync(row);

        await CreateService(store).DeleteAsync([transaction.Id]);

        Assert.Null((await store.StatementImports.GetRowByIdAsync(row.Id))!.CreatedTransactionId);
    }

    private static TransactionDeletionService CreateService(SqliteTestStore store)
    {
        return new TransactionDeletionService(store.Transactions, store.Accounts, new TransactionApplicationService(new TransactionBalanceService()));
    }

    private static async Task<Account> SaveAccountAsync(SqliteTestStore store, string name, decimal balance)
    {
        var account = new Account(Guid.NewGuid(), name, AccountType.DebitCard, balance, "RUB", DateTimeOffset.UtcNow);
        await store.Accounts.SaveAsync(account);
        return account;
    }

    // Records a transaction the way the app does: saved, and applied to the balance(s).
    private static async Task<Transaction> RecordAsync(SqliteTestStore store, Account account, decimal amount, TransactionType type, Account? destination = null)
    {
        var transaction = new Transaction(Guid.NewGuid(), new DateOnly(2026, 6, 10), amount, account.Id, null, null, type, destination?.Id, Name: $"{type} {amount}");
        var accounts = (await store.Accounts.GetAllAsync()).ToDictionary(a => a.Id);
        foreach (var updated in new TransactionApplicationService(new TransactionBalanceService()).ApplyNewTransaction(transaction, accounts))
        {
            await store.Accounts.SaveAsync(updated);
        }

        await store.Transactions.SaveAsync(transaction);
        return transaction;
    }
}
