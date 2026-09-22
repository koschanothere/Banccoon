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
        Assert.Contains(new RecurrenceSyntaxError(RecurrenceSyntaxErrorCode.FieldRequired, "FREQ"), result.Errors);
    }

    [Fact]
    public void TryParse_WhenIntervalIsInvalid_ReturnsError()
    {
        var result = service.TryParse("FREQ=DAILY;INTERVAL=0;START=2026-06-07");

        Assert.False(result.IsValid);
        Assert.Contains(RecurrenceSyntaxError.FromValidation(RecurrenceValidationErrorCode.IntervalTooLow), result.Errors);
    }

    [Fact]
    public void TryParse_WhenSyntaxIsBlank_ReturnsEmptyError()
    {
        var result = service.TryParse("   ");

        Assert.False(result.IsValid);
        Assert.Equal([new RecurrenceSyntaxError(RecurrenceSyntaxErrorCode.Empty)], result.Errors);
    }

    [Fact]
    public void TryParse_WhenFieldIsNotKeyValue_ReturnsInvalidFieldWithRawFragmentAsToken()
    {
        var result = service.TryParse("FREQ=DAILY;START=2026-06-07;garbage");

        Assert.False(result.IsValid);
        Assert.Contains(new RecurrenceSyntaxError(RecurrenceSyntaxErrorCode.InvalidField, "garbage"), result.Errors);
    }

    [Fact]
    public void TryParse_WhenFrequencyValueIsUnknown_ReturnsUnsupportedValueForThatField()
    {
        var result = service.TryParse("FREQ=HOURLY;START=2026-06-07");

        Assert.False(result.IsValid);
        Assert.Contains(new RecurrenceSyntaxError(RecurrenceSyntaxErrorCode.UnsupportedValue, "FREQ"), result.Errors);
    }

    [Fact]
    public void TryParse_WhenOptionalDateIsMalformed_ReturnsUnsupportedValueForThatField()
    {
        var result = service.TryParse("FREQ=DAILY;START=2026-06-07;UNTIL=soon");

        Assert.False(result.IsValid);
        Assert.Contains(new RecurrenceSyntaxError(RecurrenceSyntaxErrorCode.UnsupportedValue, "UNTIL"), result.Errors);
    }

    [Fact]
    public void TryParse_WhenIntervalIsNotANumber_ReturnsNotWholeNumber()
    {
        var result = service.TryParse("FREQ=DAILY;INTERVAL=two;START=2026-06-07");

        Assert.False(result.IsValid);
        Assert.Contains(new RecurrenceSyntaxError(RecurrenceSyntaxErrorCode.NotWholeNumber, "INTERVAL"), result.Errors);
    }

    [Fact]
    public void TryParse_WhenMonthDayIsNotNumberOrLast_ReturnsInvalidMonthDay()
    {
        var result = service.TryParse("FREQ=MONTHLY;START=2026-06-07;BYMONTHDAY=first");

        Assert.False(result.IsValid);
        Assert.Contains(new RecurrenceSyntaxError(RecurrenceSyntaxErrorCode.InvalidMonthDay, "BYMONTHDAY"), result.Errors);
    }

    [Fact]
    public void TryParse_WhenFieldKeyIsLowercase_ReportsTokenUppercased()
    {
        var result = service.TryParse("freq=daily;interval=x;start=2026-06-07");

        Assert.Contains(new RecurrenceSyntaxError(RecurrenceSyntaxErrorCode.NotWholeNumber, "INTERVAL"), result.Errors);
    }

    [Fact]
    public void GetExamples_ReturnsPowerUserExamples()
    {
        var examples = service.GetExamples();

        Assert.Contains(examples, example => example.Syntax.Contains("FREQ=WEEKLY", StringComparison.Ordinal));
        Assert.Contains(examples, example => example.Syntax.Contains("BYMONTHDAY=LAST", StringComparison.Ordinal));
    }
}
