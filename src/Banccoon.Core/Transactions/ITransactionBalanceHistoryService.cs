using Banccoon.Core.Models;

namespace Banccoon.Core.Transactions;

public interface ITransactionBalanceHistoryService
{
    IReadOnlyDictionary<Guid, decimal> GetBalancesAfterEachTransaction(
        Account account,
        IReadOnlyList<Transaction> accountTransactions);
}
