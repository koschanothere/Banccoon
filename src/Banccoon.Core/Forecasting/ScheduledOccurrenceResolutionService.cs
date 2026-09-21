using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public sealed class ScheduledOccurrenceResolutionService : IScheduledOccurrenceResolutionService
{
    public IReadOnlyList<ForecastEvent> ApplyOverrides(
        IReadOnlyList<ForecastEvent> projectedEvents,
        IReadOnlyList<ScheduledOccurrenceOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(projectedEvents);
        ArgumentNullException.ThrowIfNull(overrides);

        var overridesByOccurrence = overrides.ToDictionary(
            occurrenceOverride => (occurrenceOverride.ScheduledTransactionId, occurrenceOverride.OriginalOccurrenceDate));

        var resolved = new List<ForecastEvent>();
        foreach (var projectedEvent in projectedEvents)
        {
            if (!overridesByOccurrence.TryGetValue((projectedEvent.SourceId, projectedEvent.Date), out var occurrenceOverride))
            {
                resolved.Add(projectedEvent);
                continue;
            }

            if (occurrenceOverride.Kind == ScheduledOccurrenceOverrideKind.Skipped)
            {
                continue;
            }

            resolved.Add(projectedEvent with { Date = occurrenceOverride.DelayedToDate ?? projectedEvent.Date });
        }

        return resolved;
    }
}
