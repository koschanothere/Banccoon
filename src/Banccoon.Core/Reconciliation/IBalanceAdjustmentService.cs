using Banccoon.Core.Models;

namespace Banccoon.Core.Reconciliation;

public interface IBalanceAdjustmentService
{
    Transaction CreateTransaction(BalanceAdjustment adjustment);
}
