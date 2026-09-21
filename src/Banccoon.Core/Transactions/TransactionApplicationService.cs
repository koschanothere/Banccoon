using Banccoon.Core.Models;

namespace Banccoon.Core.Transactions;

public sealed class TransactionApplicationService : ITransactionApplicationService
{
    private readonly ITransactionBalanceService transactionBalanceService;

    public TransactionApplicationService(ITransactionBalanceService transactionBalanceService)
    {
        this.transactionBalanceService = transactionBalanceService;
    }

    public IReadOnlyList<Account> ApplyNewTransaction(Transaction transaction, IReadOnlyDictionary<Guid, Account> accountsById)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(accountsById);

        if (!accountsById.TryGetValue(transaction.AccountId, out var sourceAccount))
        {
            throw new ArgumentException("Transaction's account was not found.", nameof(accountsById));
        }

        var updatedSource = transactionBalanceService.Apply(sourceAccount, transaction);

        if (transaction.Type != TransactionType.Transfer || transaction.DestinationAccountId is not { } destinationAccountId)
        {
            return [updatedSource];
        }

        if (!accountsById.TryGetValue(destinationAccountId, out var destinationAccount))
        {
            throw new ArgumentException("Transfer's destination account was not found.", nameof(accountsById));
        }

        var updatedDestination = destinationAccount with
        {
            CurrentBalance = destinationAccount.CurrentBalance + Math.Abs(transaction.Amount)
        };

        return [updatedSource, updatedDestination];
    }
}
