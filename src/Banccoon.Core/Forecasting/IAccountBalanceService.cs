using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public interface IAccountBalanceService
{
    decimal GetCurrentBalance(IEnumerable<Account> accounts);
}
