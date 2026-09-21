using Banccoon.Core.Recurrence;
using Xunit;

namespace Banccoon.Tests.Recurrence;

public sealed class RecurrenceDescriptionServiceTests
{
    private readonly RecurrenceDescriptionService service = new();

    [Fact]
    public void Describe_WhenWeeklyOnMonday_ResolvesDayOfWeekFromRule()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Weekly,
            1,
            new DateOnly(2026, 6, 7),
            DayOfWeek: DayOfWeek.Monday);

        var description = service.Describe(rule);

        Assert.Equal(RecurrenceFrequency.Weekly, description.Frequency);
        Assert.Equal(1, description.Interval);
        Assert.Equal(DayOfWeek.Monday, description.ResolvedDayOfWeek);
        Assert.False(description.IsLastDayOfMonth);
        Assert.Null(description.ResolvedDayOfMonth);
        Assert.Null(description.EndDate);
    }

    [Fact]
    public void Describe_WhenWeeklyWithNoExplicitDay_FallsBackToStartDateDayOfWeek()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Weekly,
            1,
            new DateOnly(2026, 6, 7)); // a Sunday

        var description = service.Describe(rule);

        Assert.Equal(DayOfWeek.Sunday, description.ResolvedDayOfWeek);
    }

    [Fact]
    public void Describe_WhenMonthlyLastDay_SetsIsLastDayOfMonthFlag()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            1,
            new DateOnly(2026, 6, 7),
            MonthlyMode: MonthlyRecurrenceMode.LastDayOfMonth);

        var description = service.Describe(rule);

        Assert.True(description.IsLastDayOfMonth);
        Assert.Null(description.ResolvedDayOfMonth);
    }

    [Fact]
    public void Describe_WhenEveryTwoMonthsOnDay_ResolvesDayOfMonthAndInterval()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            2,
            new DateOnly(2026, 6, 7),
            DayOfMonth: 25);

        var description = service.Describe(rule);

        Assert.False(description.IsLastDayOfMonth);
        Assert.Equal(25, description.ResolvedDayOfMonth);
        Assert.Equal(2, description.Interval);
    }

    [Fact]
    public void Describe_WhenMonthlyWithNoExplicitDay_FallsBackToStartDateDay()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            1,
            new DateOnly(2026, 6, 7));

        var description = service.Describe(rule);

        Assert.Equal(7, description.ResolvedDayOfMonth);
    }

    [Fact]
    public void Describe_WhenYearlyWithEndDate_PassesThroughStartAndEndDate()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Yearly,
            1,
            new DateOnly(2026, 6, 7),
            EndDate: new DateOnly(2028, 6, 7));

        var description = service.Describe(rule);

        Assert.Equal(RecurrenceFrequency.Yearly, description.Frequency);
        Assert.Equal(new DateOnly(2026, 6, 7), description.StartDate);
        Assert.Equal(new DateOnly(2028, 6, 7), description.EndDate);
    }
}
