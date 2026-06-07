using Banccoon.Core.Recurrence;
using Xunit;

namespace Banccoon.Tests.Recurrence;

public sealed class RecurrenceServiceTests
{
    private readonly RecurrenceService service = new();

    [Fact]
    public void GetOccurrences_WhenEveryTwoDays_ReturnsEverySecondDay()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Daily,
            2,
            new DateOnly(2026, 6, 1));

        var occurrences = service.GetOccurrences(
            rule,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 7));

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 6, 1),
                new DateOnly(2026, 6, 3),
                new DateOnly(2026, 6, 5),
                new DateOnly(2026, 6, 7)
            },
            occurrences);
    }

    [Fact]
    public void GetOccurrences_WhenEveryTwoWeeksOnMonday_ReturnsMatchingMondays()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Weekly,
            2,
            new DateOnly(2026, 6, 1),
            DayOfWeek: DayOfWeek.Monday);

        var occurrences = service.GetOccurrences(
            rule,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 6, 1),
                new DateOnly(2026, 6, 15),
                new DateOnly(2026, 6, 29)
            },
            occurrences);
    }

    [Fact]
    public void GetOccurrences_WhenMonthlyDayExceedsMonthLength_ClampsToLastValidDay()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            1,
            new DateOnly(2026, 1, 31),
            DayOfMonth: 31);

        var occurrences = service.GetOccurrences(
            rule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 4, 30));

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 1, 31),
                new DateOnly(2026, 2, 28),
                new DateOnly(2026, 3, 31),
                new DateOnly(2026, 4, 30)
            },
            occurrences);
    }

    [Fact]
    public void GetOccurrences_WhenMonthlyLastDay_ReturnsLastDayOfEachMonth()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            1,
            new DateOnly(2026, 1, 1),
            MonthlyMode: MonthlyRecurrenceMode.LastDayOfMonth);

        var occurrences = service.GetOccurrences(
            rule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31));

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 1, 31),
                new DateOnly(2026, 2, 28),
                new DateOnly(2026, 3, 31)
            },
            occurrences);
    }

    [Fact]
    public void GetOccurrences_WhenYearlyStartsOnLeapDay_ClampsInNonLeapYears()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Yearly,
            1,
            new DateOnly(2024, 2, 29));

        var occurrences = service.GetOccurrences(
            rule,
            new DateOnly(2024, 1, 1),
            new DateOnly(2026, 12, 31));

        Assert.Equal(
            new[]
            {
                new DateOnly(2024, 2, 29),
                new DateOnly(2025, 2, 28),
                new DateOnly(2026, 2, 28)
            },
            occurrences);
    }

    [Fact]
    public void GetNextOccurrence_ReturnsFirstOccurrenceAfterDate()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Daily,
            2,
            new DateOnly(2026, 6, 1));

        var occurrence = service.GetNextOccurrence(rule, new DateOnly(2026, 6, 1));

        Assert.Equal(new DateOnly(2026, 6, 3), occurrence);
    }
}
