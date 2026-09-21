using Banccoon.Core.Models;

namespace Banccoon.Core.Transactions;

public interface ITransactionBalanceService
{
    Account Apply(Account account, Transaction transaction);

    Account Reverse(Account account, Transaction transaction);
}
