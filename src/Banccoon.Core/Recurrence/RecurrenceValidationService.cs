namespace Banccoon.Core.Recurrence;

public sealed class RecurrenceValidationService : IRecurrenceValidationService
{
    public RecurrenceValidationResult Validate(RecurrenceRule rule)
    {
        var errors = new List<string>();

        if (rule.Interval < 1)
        {
            errors.Add("Recurrence interval must be at least 1.");
        }

        if (rule.EndDate.HasValue && rule.EndDate.Value < rule.StartDate)
        {
            errors.Add("Recurrence end date must be on or after the start date.");
        }

        if (rule.DayOfMonth is < 1 or > 31)
        {
            errors.Add("Day of month must be between 1 and 31.");
        }

        if (rule.Frequency == RecurrenceFrequency.Monthly
            && rule.MonthlyMode == MonthlyRecurrenceMode.DayOfMonth
            && (rule.DayOfMonth ?? rule.StartDate.Day) is < 1 or > 31)
        {
            errors.Add("Monthly recurrence day must be between 1 and 31.");
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
