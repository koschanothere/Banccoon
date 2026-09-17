using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public interface IScheduledOccurrenceResolutionService
{
    IReadOnlyList<ForecastEvent> ApplyOverrides(
        IReadOnlyList<ForecastEvent> projectedEvents,
        IReadOnlyList<ScheduledOccurrenceOverride> overrides);
}
