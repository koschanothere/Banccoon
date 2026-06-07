using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.Core.Reconciliation;

public sealed class CheckInService : ICheckInService
{
    private readonly IScheduledTransactionProjectionService scheduledTransactionProjectionService;

    public CheckInService(IScheduledTransactionProjectionService scheduledTransactionProjectionService)
    {
        this.scheduledTransactionProjectionService = scheduledTransactionProjectionService;
    }

    public CheckInSession CreateSession(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        IEnumerable<ScheduledTransaction> scheduledTransactions)
    {
        if (toInclusive < fromInclusive)
        {
            throw new ArgumentException("Check-in end date must be on or after the start date.", nameof(toInclusive));
        }

        var expectedTransactions = scheduledTransactionProjectionService
            .Project(scheduledTransactions, fromInclusive, toInclusive)
            .Select(forecastEvent => new ExpectedTransactionReview(forecastEvent))
            .ToArray();

        return new CheckInSession(
            Guid.NewGuid(),
            fromInclusive,
            toInclusive,
            expectedTransactions);
    }
}
