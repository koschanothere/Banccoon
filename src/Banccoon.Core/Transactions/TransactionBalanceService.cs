using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.Core.Transactions;

public sealed class TransactionBalanceService : ITransactionBalanceService
{
    public Account Apply(Account account, Transaction transaction)
    {
        ValidateAccountMatch(account, transaction);

        return account with
        {
            CurrentBalance = account.CurrentBalance + GetSourceSignedAmount(transaction)
        };
    }

    public Account Reverse(Account account, Transaction transaction)
    {
        ValidateAccountMatch(account, transaction);

        return account with
        {
            CurrentBalance = account.CurrentBalance - GetSourceSignedAmount(transaction)
        };
    }

    private static decimal GetSourceSignedAmount(Transaction transaction)
    {
        return transaction.Type == TransactionType.Transfer
            ? -Math.Abs(transaction.Amount)
            : MoneyFlow.GetSignedAmount(transaction.Amount, transaction.Type);
    }

    private static void ValidateAccountMatch(Account account, Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(transaction);

        if (account.Id != transaction.AccountId)
        {
            throw new ArgumentException("Transaction account does not match the account being updated.", nameof(transaction));
        }
    }
}
