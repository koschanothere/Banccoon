using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Xunit;

namespace Banccoon.Tests.Forecasting;

public sealed class ScheduledOccurrenceResolutionServiceTests
{
    private readonly ScheduledOccurrenceResolutionService service = new();

    [Fact]
    public void ApplyOverrides_NoOverrides_ReturnsEventsUnchanged()
    {
        var scheduleId = Guid.NewGuid();
        var events = new[] { CreateEvent(scheduleId, new DateOnly(2026, 6, 10)) };

        var result = service.ApplyOverrides(events, []);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 6, 10), result[0].Date);
    }

    [Fact]
    public void ApplyOverrides_SkippedOccurrence_IsExcluded()
    {
        var scheduleId = Guid.NewGuid();
        var occurrenceDate = new DateOnly(2026, 6, 10);
        var events = new[] { CreateEvent(scheduleId, occurrenceDate) };
        var overrides = new[]
        {
            new ScheduledOccurrenceOverride(Guid.NewGuid(), scheduleId, occurrenceDate, ScheduledOccurrenceOverrideKind.Skipped)
        };

        var result = service.ApplyOverrides(events, overrides);

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyOverrides_DelayedOccurrence_MovesToNewDate()
    {
        var scheduleId = Guid.NewGuid();
        var occurrenceDate = new DateOnly(2026, 6, 10);
        var delayedTo = new DateOnly(2026, 6, 17);
        var events = new[] { CreateEvent(scheduleId, occurrenceDate) };
        var overrides = new[]
        {
            new ScheduledOccurrenceOverride(
                Guid.NewGuid(), scheduleId, occurrenceDate, ScheduledOccurrenceOverrideKind.Delayed, delayedTo)
        };

        var result = service.ApplyOverrides(events, overrides);

        Assert.Single(result);
        Assert.Equal(delayedTo, result[0].Date);
    }

    [Fact]
    public void ApplyOverrides_OverrideForDifferentOccurrence_DoesNotAffectOtherOccurrences()
    {
        var scheduleId = Guid.NewGuid();
        var events = new[]
        {
            CreateEvent(scheduleId, new DateOnly(2026, 6, 10)),
            CreateEvent(scheduleId, new DateOnly(2026, 7, 10))
        };
        var overrides = new[]
        {
            new ScheduledOccurrenceOverride(
                Guid.NewGuid(), scheduleId, new DateOnly(2026, 6, 10), ScheduledOccurrenceOverrideKind.Skipped)
        };

        var result = service.ApplyOverrides(events, overrides);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 7, 10), result[0].Date);
    }

    private static ForecastEvent CreateEvent(Guid scheduleId, DateOnly date)
    {
        return new ForecastEvent(
            scheduleId,
            date,
            "Rent",
            1000m,
            TransactionType.Expense,
            Guid.NewGuid(),
            CategoryId: null,
            ForecastEventKind.ScheduledTransaction);
    }
}
