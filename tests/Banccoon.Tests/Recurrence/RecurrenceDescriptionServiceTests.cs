using Banccoon.Core.Recurrence;
using Xunit;

namespace Banccoon.Tests.Recurrence;

public sealed class RecurrenceDescriptionServiceTests
{
    private readonly RecurrenceDescriptionService service = new();

    [Fact]
    public void Describe_WhenWeeklyOnMonday_ReturnsNaturalSentence()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Weekly,
            1,
            new DateOnly(2026, 6, 7),
            DayOfWeek: DayOfWeek.Monday);

        var description = service.Describe(rule);

        Assert.Equal("Every week on Monday", description);
    }

    [Fact]
    public void Describe_WhenMonthlyLastDay_ReturnsLastDaySentence()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            1,
            new DateOnly(2026, 6, 7),
            MonthlyMode: MonthlyRecurrenceMode.LastDayOfMonth);

        var description = service.Describe(rule);

        Assert.Equal("Every month on the last day", description);
    }

    [Fact]
    public void Describe_WhenEveryTwoMonthsOnDay_ReturnsIntervalSentence()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            2,
            new DateOnly(2026, 6, 7),
            DayOfMonth: 25);

        var description = service.Describe(rule);

        Assert.Equal("Every 2 months on day 25", description);
    }

    [Fact]
    public void Describe_WhenYearlyWithEndDate_IncludesDateAndUntilClause()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Yearly,
            1,
            new DateOnly(2026, 6, 7),
            EndDate: new DateOnly(2028, 6, 7));

        var description = service.Describe(rule);

        Assert.Equal("Every year on June 7 until June 7, 2028", description);
    }
}
