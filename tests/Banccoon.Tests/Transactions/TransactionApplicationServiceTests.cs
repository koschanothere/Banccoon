using Banccoon.Core.Models;
using Banccoon.Core.Transactions;
using Xunit;

namespace Banccoon.Tests.Transactions;

public sealed class TransactionApplicationServiceTests
{
    private readonly TransactionApplicationService service = new(new TransactionBalanceService());

    [Fact]
    public void ApplyNewTransaction_Expense_UpdatesOnlyThatAccount()
    {
        var account = CreateAccount(100m);
        var transaction = CreateTransaction(account.Id, 40m, TransactionType.Expense);

        var updated = service.ApplyNewTransaction(transaction, new Dictionary<Guid, Account> { [account.Id] = account });

        Assert.Single(updated);
        Assert.Equal(60m, updated[0].CurrentBalance);
    }

    [Theory]
    [InlineData(TransactionType.Expense)]
    [InlineData(TransactionType.Income)]
    public void ReverseTransaction_UndoesApplyNewTransaction(TransactionType type)
    {
        var account = CreateAccount(100m);
        var transaction = CreateTransaction(account.Id, 40m, type);
        var applied = service.ApplyNewTransaction(transaction, new Dictionary<Guid, Account> { [account.Id] = account });

        var reversed = service.ReverseTransaction(transaction, applied.ToDictionary(a => a.Id));

        Assert.Equal(account, Assert.Single(reversed));
    }

    [Fact]
    public void ReverseTransaction_Transfer_RestoresBothAccounts()
    {
        var source = CreateAccount(100m);
        var destination = CreateAccount(20m);
        var transaction = CreateTransaction(source.Id, 30m, TransactionType.Transfer, destination.Id);
        var applied = service.ApplyNewTransaction(transaction, new Dictionary<Guid, Account> { [source.Id] = source, [destination.Id] = destination });

        var reversed = service.ReverseTransaction(transaction, applied.ToDictionary(a => a.Id));

        Assert.Equal(source, reversed.Single(a => a.Id == source.Id));
        Assert.Equal(destination, reversed.Single(a => a.Id == destination.Id));
    }

    [Fact]
    public void ApplyNewTransaction_Transfer_DebitsSourceAndCreditsDestination()
    {
        var source = CreateAccount(100m);
        var destination = CreateAccount(20m);
        var transaction = CreateTransaction(source.Id, 30m, TransactionType.Transfer, destination.Id);

        var updated = service.ApplyNewTransaction(transaction, new Dictionary<Guid, Account>
        {
            [source.Id] = source,
            [destination.Id] = destination
        });

        Assert.Equal(2, updated.Count);
        Assert.Equal(70m, updated.Single(account => account.Id == source.Id).CurrentBalance);
        Assert.Equal(50m, updated.Single(account => account.Id == destination.Id).CurrentBalance);
    }

    [Fact]
    public void ApplyNewTransaction_MissingSourceAccount_Throws()
    {
        var transaction = CreateTransaction(Guid.NewGuid(), 10m, TransactionType.Income);

        Assert.Throws<ArgumentException>(() => service.ApplyNewTransaction(transaction, new Dictionary<Guid, Account>()));
    }

    [Fact]
    public void ApplyNewTransaction_TransferMissingDestinationAccount_Throws()
    {
        var source = CreateAccount(100m);
        var transaction = CreateTransaction(source.Id, 10m, TransactionType.Transfer, Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => service.ApplyNewTransaction(
            transaction,
            new Dictionary<Guid, Account> { [source.Id] = source }));
    }

    private static Account CreateAccount(decimal balance)
    {
        return new Account(
            Guid.NewGuid(),
            "Checking",
            AccountType.DebitCard,
            balance,
            "EUR",
            DateTimeOffset.UtcNow);
    }

    private static Transaction CreateTransaction(
        Guid accountId,
        decimal amount,
        TransactionType type,
        Guid? destinationAccountId = null)
    {
        return new Transaction(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 8),
            amount,
            accountId,
            CategoryId: null,
            Notes: null,
            type,
            destinationAccountId);
    }
}
