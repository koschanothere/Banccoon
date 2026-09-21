using Banccoon.Core.Recurrence;
using Xunit;

namespace Banccoon.Tests.Recurrence;

public sealed class RecurrenceDescriptionServiceTests
{
    private readonly RecurrenceDescriptionService service = new();

    [Fact]
    public void Describe_WhenWeeklyOnMonday_ResolvesDayOfWeek()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Weekly,
            1,
            new DateOnly(2026, 6, 7),
            DayOfWeek: DayOfWeek.Monday);

        var description = service.Describe(rule);

        Assert.Equal(RecurrenceFrequency.Weekly, description.Frequency);
        Assert.Equal(1, description.Interval);
        Assert.False(description.IsLastDayOfMonth);
        Assert.Equal(DayOfWeek.Monday, description.ResolvedDayOfWeek);
        Assert.Null(description.ResolvedDayOfMonth);
        Assert.Null(description.EndDate);
    }

    [Fact]
    public void Describe_WhenMonthlyLastDay_SetsIsLastDayOfMonth()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            1,
            new DateOnly(2026, 6, 7),
            MonthlyMode: MonthlyRecurrenceMode.LastDayOfMonth);

        var description = service.Describe(rule);

        Assert.Equal(RecurrenceFrequency.Monthly, description.Frequency);
        Assert.True(description.IsLastDayOfMonth);
        Assert.Null(description.ResolvedDayOfMonth);
        Assert.Null(description.ResolvedDayOfWeek);
    }

    [Fact]
    public void Describe_WhenEveryTwoMonthsOnDay_ResolvesDayOfMonth()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            2,
            new DateOnly(2026, 6, 7),
            DayOfMonth: 25);

        var description = service.Describe(rule);

        Assert.Equal(2, description.Interval);
        Assert.False(description.IsLastDayOfMonth);
        Assert.Equal(25, description.ResolvedDayOfMonth);
    }

    [Fact]
    public void Describe_WhenYearlyWithEndDate_CarriesStartAndEndDate()
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
        Assert.Null(description.ResolvedDayOfWeek);
        Assert.Null(description.ResolvedDayOfMonth);
    }
}
