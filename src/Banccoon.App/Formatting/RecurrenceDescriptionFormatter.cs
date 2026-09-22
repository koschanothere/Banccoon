using System.Globalization;
using Banccoon.Core.Recurrence;

namespace Banccoon.App.Formatting;

// Turns Core's language-agnostic RecurrenceDescriptionData into display text. Reproduces the
// exact English sentences RecurrenceDescriptionService used to compose directly - this is the
// hook point for translated output once the Translator/resx foundation is wired in.
public static class RecurrenceDescriptionFormatter
{
    public static string Format(RecurrenceDescriptionData data)
    {
        var coreDescription = data.Frequency switch
        {
            RecurrenceFrequency.Daily => DescribeDaily(data),
            RecurrenceFrequency.Weekly => DescribeWeekly(data),
            RecurrenceFrequency.Monthly => DescribeMonthly(data),
            RecurrenceFrequency.Yearly => DescribeYearly(data),
            _ => throw new NotSupportedException($"Unsupported recurrence frequency: {data.Frequency}")
        };

        return data.EndDate.HasValue
            ? $"{coreDescription} until {FormatDate(data.EndDate.Value)}"
            : coreDescription;
    }

    private static string DescribeDaily(RecurrenceDescriptionData data)
    {
        return data.Interval == 1
            ? "Every day"
            : $"Every {data.Interval} days";
    }

    private static string DescribeWeekly(RecurrenceDescriptionData data)
    {
        var dayOfWeek = data.ResolvedDayOfWeek!.Value;

        return data.Interval == 1
            ? $"Every week on {dayOfWeek}"
            : $"Every {data.Interval} weeks on {dayOfWeek}";
    }

    private static string DescribeMonthly(RecurrenceDescriptionData data)
    {
        if (data.IsLastDayOfMonth)
        {
            return data.Interval == 1
                ? "Every month on the last day"
                : $"Every {data.Interval} months on the last day";
        }

        var dayOfMonth = data.ResolvedDayOfMonth!.Value;

        return data.Interval == 1
            ? $"Every month on day {dayOfMonth}"
            : $"Every {data.Interval} months on day {dayOfMonth}";
    }

    private static string DescribeYearly(RecurrenceDescriptionData data)
    {
        var dateDescription = $"{data.StartDate:MMMM} {data.StartDate.Day}";

        return data.Interval == 1
            ? $"Every year on {dateDescription}"
            : $"Every {data.Interval} years on {dateDescription}";
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
    }
}
