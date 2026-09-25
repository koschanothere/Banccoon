using Banccoon.Core.Models;

namespace Banccoon.Core.Transactions;

public interface ITransactionApplicationService
{
    IReadOnlyList<Account> ApplyNewTransaction(Transaction transaction, IReadOnlyDictionary<Guid, Account> accountsById);

    // The exact inverse of ApplyNewTransaction: undoes the transaction's effect on its account and,
    // for a transfer, on its destination account too.
    IReadOnlyList<Account> ReverseTransaction(Transaction transaction, IReadOnlyDictionary<Guid, Account> accountsById);
}
