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

    public string Describe(RecurrenceRule rule)
    {
        recurrenceValidationService.ThrowIfInvalid(rule);

        var coreDescription = rule.Frequency switch
        {
            RecurrenceFrequency.Daily => DescribeDaily(rule),
            RecurrenceFrequency.Weekly => DescribeWeekly(rule),
            RecurrenceFrequency.Monthly => DescribeMonthly(rule),
            RecurrenceFrequency.Yearly => DescribeYearly(rule),
            _ => throw new NotSupportedException($"Unsupported recurrence frequency: {rule.Frequency}")
        };

        return rule.EndDate.HasValue
            ? $"{coreDescription} until {FormatDate(rule.EndDate.Value)}"
            : coreDescription;
    }

    private static string DescribeDaily(RecurrenceRule rule)
    {
        return rule.Interval == 1
            ? "Every day"
            : $"Every {rule.Interval} days";
    }

    private static string DescribeWeekly(RecurrenceRule rule)
    {
        var dayOfWeek = rule.DayOfWeek ?? rule.StartDate.DayOfWeek;

        return rule.Interval == 1
            ? $"Every week on {dayOfWeek}"
            : $"Every {rule.Interval} weeks on {dayOfWeek}";
    }

    private static string DescribeMonthly(RecurrenceRule rule)
    {
        if (rule.MonthlyMode == MonthlyRecurrenceMode.LastDayOfMonth)
        {
            return rule.Interval == 1
                ? "Every month on the last day"
                : $"Every {rule.Interval} months on the last day";
        }

        var dayOfMonth = rule.DayOfMonth ?? rule.StartDate.Day;

        return rule.Interval == 1
            ? $"Every month on day {dayOfMonth}"
            : $"Every {rule.Interval} months on day {dayOfMonth}";
    }

    private static string DescribeYearly(RecurrenceRule rule)
    {
        var dateDescription = $"{rule.StartDate:MMMM} {rule.StartDate.Day}";

        return rule.Interval == 1
            ? $"Every year on {dateDescription}"
            : $"Every {rule.Interval} years on {dateDescription}";
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }
}
