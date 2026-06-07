using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;

namespace Banccoon.Core.Forecasting;

public sealed class ScheduledTransactionProjectionService : IScheduledTransactionProjectionService
{
    private readonly IRecurrenceService recurrenceService;

    public ScheduledTransactionProjectionService(IRecurrenceService recurrenceService)
    {
        this.recurrenceService = recurrenceService;
    }

    public IReadOnlyList<ForecastEvent> Project(
        IEnumerable<ScheduledTransaction> scheduledTransactions,
        DateOnly fromInclusive,
        DateOnly toInclusive)
    {
        ArgumentNullException.ThrowIfNull(scheduledTransactions);

        var events = new List<ForecastEvent>();

        foreach (var scheduledTransaction in scheduledTransactions.Where(item => item.Active))
        {
            var projectionStart = Max(fromInclusive, scheduledTransaction.NextOccurrence);
            var occurrences = recurrenceService.GetOccurrences(
                scheduledTransaction.RecurrenceRule,
                projectionStart,
                toInclusive);

            foreach (var occurrence in occurrences)
            {
                events.Add(new ForecastEvent(
                    scheduledTransaction.Id,
                    occurrence,
                    scheduledTransaction.Name,
                    Math.Abs(scheduledTransaction.Amount),
                    scheduledTransaction.Type,
                    scheduledTransaction.AccountId,
                    scheduledTransaction.CategoryId,
                    ForecastEventKind.ScheduledTransaction));
            }
        }

        return events
            .OrderBy(forecastEvent => forecastEvent.Date)
            .ThenBy(forecastEvent => forecastEvent.SignedAmount)
            .ThenBy(forecastEvent => forecastEvent.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DateOnly Max(DateOnly left, DateOnly right)
    {
        return left >= right ? left : right;
    }
}
