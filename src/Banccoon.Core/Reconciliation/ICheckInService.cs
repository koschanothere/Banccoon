using Banccoon.Core.Models;

namespace Banccoon.Core.Reconciliation;

public interface ICheckInService
{
    CheckInSession CreateSession(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        IEnumerable<ScheduledTransaction> scheduledTransactions);
}
