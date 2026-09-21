using Banccoon.Core.Models;

namespace Banccoon.Core.Transactions;

public sealed class TransactionBalanceHistoryService : ITransactionBalanceHistoryService
{
    private readonly ITransactionBalanceService transactionBalanceService;

    public TransactionBalanceHistoryService(ITransactionBalanceService transactionBalanceService)
    {
        this.transactionBalanceService = transactionBalanceService;
    }

    public IReadOnlyDictionary<Guid, decimal> GetBalancesAfterEachTransaction(
        Account account,
        IReadOnlyList<Transaction> accountTransactions)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(accountTransactions);

        var ordered = accountTransactions.OrderByDescending(transaction => transaction.Date).ToList();
        var result = new Dictionary<Guid, decimal>();
        var balance = account.CurrentBalance;

        foreach (var transaction in ordered)
        {
            result[transaction.Id] = balance;

            if (transaction.AccountId == account.Id)
            {
                balance = transactionBalanceService.Reverse(account with { CurrentBalance = balance }, transaction).CurrentBalance;
            }
            else if (transaction.DestinationAccountId == account.Id)
            {
                balance -= Math.Abs(transaction.Amount);
            }
            else
            {
                throw new ArgumentException(
                    "Transaction is not related to the given account.",
                    nameof(accountTransactions));
            }
        }

        return result;
    }
}
