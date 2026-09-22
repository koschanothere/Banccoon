namespace Banccoon.Core.Recurrence;

public sealed class RecurrenceValidationService : IRecurrenceValidationService
{
    public RecurrenceValidationResult Validate(RecurrenceRule rule)
    {
        var errors = new List<RecurrenceValidationErrorCode>();

        if (rule.Interval < 1)
        {
            errors.Add(RecurrenceValidationErrorCode.IntervalTooLow);
        }

        if (rule.EndDate.HasValue && rule.EndDate.Value < rule.StartDate)
        {
            errors.Add(RecurrenceValidationErrorCode.EndDateBeforeStartDate);
        }

        if (rule.DayOfMonth is < 1 or > 31)
        {
            errors.Add(RecurrenceValidationErrorCode.DayOfMonthOutOfRange);
        }

        if (rule.Frequency == RecurrenceFrequency.Monthly
            && rule.MonthlyMode == MonthlyRecurrenceMode.DayOfMonth
            && (rule.DayOfMonth ?? rule.StartDate.Day) is < 1 or > 31)
        {
            errors.Add(RecurrenceValidationErrorCode.MonthlyDayOutOfRange);
        }

        return errors.Count == 0
            ? RecurrenceValidationResult.Success
            : new RecurrenceValidationResult(errors);
    }

    public void ThrowIfInvalid(RecurrenceRule rule)
    {
        var result = Validate(rule);
        if (!result.IsValid)
        {
            throw new RecurrenceValidationException(result.Errors);
        }
    }
}
