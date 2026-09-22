using Banccoon.Core.Recurrence;
using Xunit;

namespace Banccoon.Tests.Recurrence;

public sealed class RecurrenceValidationServiceTests
{
    private readonly RecurrenceValidationService service = new();

    [Fact]
    public void Validate_WhenRuleIsValid_ReturnsSuccess()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            1,
            new DateOnly(2026, 6, 7),
            DayOfMonth: 10);

        var result = service.Validate(rule);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenIntervalIsZero_ReturnsError()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Daily,
            0,
            new DateOnly(2026, 6, 7));

        var result = service.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(RecurrenceValidationErrorCode.IntervalTooLow, result.Errors);
    }

    [Fact]
    public void Validate_WhenEndDateIsBeforeStartDate_ReturnsError()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Weekly,
            1,
            new DateOnly(2026, 6, 7),
            EndDate: new DateOnly(2026, 6, 6));

        var result = service.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(RecurrenceValidationErrorCode.EndDateBeforeStartDate, result.Errors);
    }

    [Fact]
    public void Validate_WhenDayOfMonthIsOutsideSupportedRange_ReturnsError()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            1,
            new DateOnly(2026, 6, 7),
            DayOfMonth: 32);

        var result = service.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(RecurrenceValidationErrorCode.DayOfMonthOutOfRange, result.Errors);
    }

    [Fact]
    public void ThrowIfInvalid_WhenRuleIsInvalid_ThrowsValidationException()
    {
        var rule = new RecurrenceRule(
            RecurrenceFrequency.Daily,
            0,
            new DateOnly(2026, 6, 7));

        var exception = Assert.Throws<RecurrenceValidationException>(() => service.ThrowIfInvalid(rule));

        Assert.Contains(RecurrenceValidationErrorCode.IntervalTooLow, exception.Errors);
    }
}
