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

        var isLastDayOfMonth = rule.Frequency == RecurrenceFrequency.Monthly
            && rule.MonthlyMode == MonthlyRecurrenceMode.LastDayOfMonth;

        var resolvedDayOfWeek = rule.Frequency == RecurrenceFrequency.Weekly
            ? rule.DayOfWeek ?? rule.StartDate.DayOfWeek
            : (DayOfWeek?)null;

        var resolvedDayOfMonth = rule.Frequency == RecurrenceFrequency.Monthly && !isLastDayOfMonth
            ? rule.DayOfMonth ?? rule.StartDate.Day
            : (int?)null;

        return new RecurrenceDescriptionData(
            rule.Frequency,
            rule.Interval,
            isLastDayOfMonth,
            resolvedDayOfWeek,
            resolvedDayOfMonth,
            rule.StartDate,
            rule.EndDate);
    }
}
