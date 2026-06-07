using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public sealed class AccountBalanceService : IAccountBalanceService
{
    public decimal GetCurrentBalance(IEnumerable<Account> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        return accounts
            .Where(account => !account.IsArchived)
            .Sum(account => account.CurrentBalance);
    }
}
