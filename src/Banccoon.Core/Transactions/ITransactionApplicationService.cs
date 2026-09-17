using Banccoon.Core.Models;

namespace Banccoon.Core.Transactions;

public interface ITransactionApplicationService
{
    IReadOnlyList<Account> ApplyNewTransaction(Transaction transaction, IReadOnlyDictionary<Guid, Account> accountsById);
}
