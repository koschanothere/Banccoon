using System.Globalization;
using Banccoon.Core.Recurrence;

namespace Banccoon.App.Formatting;

public static class RecurrenceDescriptionFormatter
{
    public static string Format(RecurrenceDescriptionData data)
    {
        var coreDescription = data.Frequency switch
        {
            RecurrenceFrequency.Daily => FormatDaily(data),
            RecurrenceFrequency.Weekly => FormatWeekly(data),
            RecurrenceFrequency.Monthly => FormatMonthly(data),
            RecurrenceFrequency.Yearly => FormatYearly(data),
            _ => throw new NotSupportedException($"Unsupported recurrence frequency: {data.Frequency}")
        };

        return data.EndDate.HasValue
            ? $"{coreDescription} until {FormatDate(data.EndDate.Value)}"
            : coreDescription;
    }

    private static string FormatDaily(RecurrenceDescriptionData data)
    {
        return data.Interval == 1
            ? "Every day"
            : $"Every {data.Interval} days";
    }

    private static string FormatWeekly(RecurrenceDescriptionData data)
    {
        return data.Interval == 1
            ? $"Every week on {data.ResolvedDayOfWeek}"
            : $"Every {data.Interval} weeks on {data.ResolvedDayOfWeek}";
    }

    private static string FormatMonthly(RecurrenceDescriptionData data)
    {
        if (data.IsLastDayOfMonth)
        {
            return data.Interval == 1
                ? "Every month on the last day"
                : $"Every {data.Interval} months on the last day";
        }

        return data.Interval == 1
            ? $"Every month on day {data.ResolvedDayOfMonth}"
            : $"Every {data.Interval} months on day {data.ResolvedDayOfMonth}";
    }

    private static string FormatYearly(RecurrenceDescriptionData data)
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
