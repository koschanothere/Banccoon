using Banccoon.Core.Models;

namespace Banccoon.Core.Reconciliation;

public interface IGroupedSpendingService
{
    Transaction CreateTransaction(GroupedSpendingEntry entry);
}
