using Banccoon.Core.Models;
using Banccoon.Core.Transactions;
using Xunit;

namespace Banccoon.Tests.Transactions;

public sealed class TransactionBalanceServiceTests
{
    private readonly TransactionBalanceService service = new();

    [Fact]
    public void Apply_Income_IncreasesAccountBalance()
    {
        var account = CreateAccount(100m);
        var transaction = CreateTransaction(account.Id, 40m, TransactionType.Income);

        var updated = service.Apply(account, transaction);

        Assert.Equal(140m, updated.CurrentBalance);
    }

    [Fact]
    public void Apply_Expense_DecreasesAccountBalance()
    {
        var account = CreateAccount(100m);
        var transaction = CreateTransaction(account.Id, 35m, TransactionType.Expense);

        var updated = service.Apply(account, transaction);

        Assert.Equal(65m, updated.CurrentBalance);
    }

    [Fact]
    public void Apply_Transfer_DecreasesSourceAccountBalance()
    {
        var account = CreateAccount(100m);
        var transaction = CreateTransaction(account.Id, 35m, TransactionType.Transfer);

        var updated = service.Apply(account, transaction);

        Assert.Equal(65m, updated.CurrentBalance);
    }

    [Fact]
    public void Reverse_ReversesTheOriginalTransactionEffect()
    {
        var account = CreateAccount(65m);
        var transaction = CreateTransaction(account.Id, 35m, TransactionType.Expense);

        var updated = service.Reverse(account, transaction);

        Assert.Equal(100m, updated.CurrentBalance);
    }

    [Fact]
    public void Apply_ThrowsWhenTransactionBelongsToDifferentAccount()
    {
        var account = CreateAccount(100m);
        var transaction = CreateTransaction(Guid.NewGuid(), 35m, TransactionType.Expense);

        Assert.Throws<ArgumentException>(() => service.Apply(account, transaction));
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

    private static Transaction CreateTransaction(Guid accountId, decimal amount, TransactionType type)
    {
        return new Transaction(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 8),
            amount,
            accountId,
            CategoryId: null,
            Notes: null,
            type);
    }
}
