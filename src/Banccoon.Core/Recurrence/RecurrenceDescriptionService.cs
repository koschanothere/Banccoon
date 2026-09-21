namespace Banccoon.Core.Recurrence;

public sealed class RecurrenceDescriptionService : IRecurrenceDescriptionService
{
    private readonly IRecurrenceValidationService recurrenceValidationService;

    public RecurrenceDescriptionService()
        : this(new RecurrenceValidationService())
    {
    }

    public RecurrenceDescriptionService(IRecurrenceValidationService recurrenceValidationService)
    {
        this.recurrenceValidationService = recurrenceValidationService;
    }

    public RecurrenceDescriptionData Describe(RecurrenceRule rule)
    {
        recurrenceValidationService.ThrowIfInvalid(rule);

        return rule.Frequency switch
        {
            RecurrenceFrequency.Daily => new RecurrenceDescriptionData(
                rule.Frequency, rule.Interval, false, null, null, rule.StartDate, rule.EndDate),
            RecurrenceFrequency.Weekly => new RecurrenceDescriptionData(
                rule.Frequency, rule.Interval, false, rule.DayOfWeek ?? rule.StartDate.DayOfWeek, null, rule.StartDate, rule.EndDate),
            RecurrenceFrequency.Monthly => DescribeMonthly(rule),
            RecurrenceFrequency.Yearly => new RecurrenceDescriptionData(
                rule.Frequency, rule.Interval, false, null, null, rule.StartDate, rule.EndDate),
            _ => throw new NotSupportedException($"Unsupported recurrence frequency: {rule.Frequency}")
        };
    }

    private static RecurrenceDescriptionData DescribeMonthly(RecurrenceRule rule)
    {
        if (rule.MonthlyMode == MonthlyRecurrenceMode.LastDayOfMonth)
        {
            return new RecurrenceDescriptionData(
                rule.Frequency, rule.Interval, true, null, null, rule.StartDate, rule.EndDate);
        }

        return new RecurrenceDescriptionData(
            rule.Frequency, rule.Interval, false, null, rule.DayOfMonth ?? rule.StartDate.Day, rule.StartDate, rule.EndDate);
    }
}
