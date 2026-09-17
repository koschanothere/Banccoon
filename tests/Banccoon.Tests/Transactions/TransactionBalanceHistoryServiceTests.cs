using Banccoon.Core.Models;
using Banccoon.Core.Transactions;
using Xunit;

namespace Banccoon.Tests.Transactions;

public sealed class TransactionBalanceHistoryServiceTests
{
    private readonly TransactionBalanceHistoryService service = new(new TransactionBalanceService());

    [Fact]
    public void GetBalancesAfterEachTransaction_ReconstructsRunningBalanceBackward()
    {
        var account = CreateAccount(130m);
        var older = CreateTransaction(account.Id, new DateOnly(2026, 6, 1), 20m, TransactionType.Expense);
        var newer = CreateTransaction(account.Id, new DateOnly(2026, 6, 5), 50m, TransactionType.Income);

        var result = service.GetBalancesAfterEachTransaction(account, [older, newer]);

        Assert.Equal(130m, result[newer.Id]);
        Assert.Equal(80m, result[older.Id]);
    }

    [Fact]
    public void GetBalancesAfterEachTransaction_TransferAsDestination_SubtractsCreditGoingBackward()
    {
        var account = CreateAccount(150m);
        var incomingTransfer = CreateTransaction(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 5),
            50m,
            TransactionType.Transfer,
            destinationAccountId: account.Id);

        var result = service.GetBalancesAfterEachTransaction(account, [incomingTransfer]);

        Assert.Equal(150m, result[incomingTransfer.Id]);
    }

    [Fact]
    public void GetBalancesAfterEachTransaction_UnrelatedTransaction_Throws()
    {
        var account = CreateAccount(100m);
        var unrelated = CreateTransaction(Guid.NewGuid(), new DateOnly(2026, 6, 1), 10m, TransactionType.Expense);

        Assert.Throws<ArgumentException>(() => service.GetBalancesAfterEachTransaction(account, [unrelated]));
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
        DateOnly date,
        decimal amount,
        TransactionType type,
        Guid? destinationAccountId = null)
    {
        return new Transaction(
            Guid.NewGuid(),
            date,
            amount,
            accountId,
            CategoryId: null,
            Notes: null,
            type,
            destinationAccountId);
    }
}
