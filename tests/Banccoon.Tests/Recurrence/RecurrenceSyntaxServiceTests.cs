using Banccoon.Core.Recurrence;
using Xunit;

namespace Banccoon.Tests.Recurrence;

public sealed class RecurrenceSyntaxServiceTests
{
    private readonly RecurrenceSyntaxService service = new();

    [Fact]
    public void Format_WhenWeeklyRule_IncludesFrequencyIntervalStartAndDay()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Weekly,
            2,
            new DateOnly(2026, 6, 7),
            DayOfWeek: DayOfWeek.Monday);

        var syntax = service.Format(rule);

        Assert.Equal("FREQ=WEEKLY;INTERVAL=2;START=2026-06-07;BYDAY=MO", syntax);
    }

    [Fact]
    public void TryParse_WhenWeeklySyntax_ReturnsStructuredRule()
    {
        var result = service.TryParse("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO;START=2026-06-07");

        Assert.True(result.IsValid);
        Assert.Equal(new RecurrenceRule(
            RecurrenceFrequency.Weekly,
            2,
            new DateOnly(2026, 6, 7),
            DayOfWeek: DayOfWeek.Monday), result.Rule);
    }

    [Fact]
    public void TryParse_WhenMonthlyLastDaySyntax_ReturnsLastDayRule()
    {
        var result = service.TryParse("FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=LAST;START=2026-06-07");

        Assert.True(result.IsValid);
        Assert.Equal(MonthlyRecurrenceMode.LastDayOfMonth, result.Rule?.MonthlyMode);
    }

    [Fact]
    public void TryParse_WhenEndDateIsIncluded_ReturnsRuleWithEndDate()
    {
        var result = service.TryParse("FREQ=DAILY;INTERVAL=1;START=2026-06-07;UNTIL=2026-06-14");

        Assert.True(result.IsValid);
        Assert.Equal(new DateOnly(2026, 6, 14), result.Rule?.EndDate);
    }

    [Fact]
    public void TryParse_WhenFrequencyIsMissing_ReturnsError()
    {
        var result = service.TryParse("INTERVAL=1;START=2026-06-07");

        Assert.False(result.IsValid);
        Assert.Contains("FREQ is required.", result.Errors);
    }

    [Fact]
    public void TryParse_WhenIntervalIsInvalid_ReturnsError()
    {
        var result = service.TryParse("FREQ=DAILY;INTERVAL=0;START=2026-06-07");

        Assert.False(result.IsValid);
        Assert.Contains("Recurrence interval must be at least 1.", result.Errors);
    }

    [Fact]
    public void GetExamples_ReturnsPowerUserExamples()
    {
        var examples = service.GetExamples();

        Assert.Contains(examples, example => example.Syntax.Contains("FREQ=WEEKLY", StringComparison.Ordinal));
        Assert.Contains(examples, example => example.Syntax.Contains("BYMONTHDAY=LAST", StringComparison.Ordinal));
    }
}
