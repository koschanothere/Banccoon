namespace Banccoon.Core.Recurrence;

public sealed class RecurrenceService : IRecurrenceService
{
    private const int NextOccurrenceSearchYears = 100;

    public IReadOnlyList<DateOnly> GetOccurrences(
        RecurrenceRule rule,
        DateOnly fromInclusive,
        DateOnly toInclusive)
    {
        Validate(rule);

        if (fromInclusive > toInclusive)
        {
            return Array.Empty<DateOnly>();
        }

        var effectiveEnd = Min(toInclusive, rule.EndDate ?? toInclusive);
        if (effectiveEnd < rule.StartDate)
        {
            return Array.Empty<DateOnly>();
        }

        return rule.Frequency switch
        {
            RecurrenceFrequency.Daily => GetDailyOccurrences(rule, fromInclusive, effectiveEnd),
            RecurrenceFrequency.Weekly => GetWeeklyOccurrences(rule, fromInclusive, effectiveEnd),
            RecurrenceFrequency.Monthly => GetMonthlyOccurrences(rule, fromInclusive, effectiveEnd),
            RecurrenceFrequency.Yearly => GetYearlyOccurrences(rule, fromInclusive, effectiveEnd),
            _ => throw new NotSupportedException($"Unsupported recurrence frequency: {rule.Frequency}")
        };
    }

    public DateOnly? GetNextOccurrence(RecurrenceRule rule, DateOnly afterDate)
    {
        var from = afterDate.AddDays(1);
        var to = afterDate.AddYears(NextOccurrenceSearchYears);

        var occurrences = GetOccurrences(rule, from, to);
        return occurrences.Count > 0 ? occurrences[0] : null;
    }

    public string Describe(RecurrenceRule rule)
    {
        Validate(rule);

        return rule.Frequency switch
        {
            RecurrenceFrequency.Daily when rule.Interval == 1 => "Every day",
            RecurrenceFrequency.Daily => $"Every {rule.Interval} days",
            RecurrenceFrequency.Weekly when rule.Interval == 1 => $"Every week on {rule.DayOfWeek ?? rule.StartDate.DayOfWeek}",
            RecurrenceFrequency.Weekly => $"Every {rule.Interval} weeks on {rule.DayOfWeek ?? rule.StartDate.DayOfWeek}",
            RecurrenceFrequency.Monthly when rule.MonthlyMode == MonthlyRecurrenceMode.LastDayOfMonth && rule.Interval == 1 => "Every month on the last day",
            RecurrenceFrequency.Monthly when rule.MonthlyMode == MonthlyRecurrenceMode.LastDayOfMonth => $"Every {rule.Interval} months on the last day",
            RecurrenceFrequency.Monthly when rule.Interval == 1 => $"Every month on day {rule.DayOfMonth ?? rule.StartDate.Day}",
            RecurrenceFrequency.Monthly => $"Every {rule.Interval} months on day {rule.DayOfMonth ?? rule.StartDate.Day}",
            RecurrenceFrequency.Yearly when rule.Interval == 1 => "Every year",
            RecurrenceFrequency.Yearly => $"Every {rule.Interval} years",
            _ => throw new NotSupportedException($"Unsupported recurrence frequency: {rule.Frequency}")
        };
    }

    private static IReadOnlyList<DateOnly> GetDailyOccurrences(
        RecurrenceRule rule,
        DateOnly fromInclusive,
        DateOnly toInclusive)
    {
        var occurrences = new List<DateOnly>();
        var daysFromStart = Math.Max(0, fromInclusive.DayNumber - rule.StartDate.DayNumber);
        var intervalsToSkip = CeilingDivide(daysFromStart, rule.Interval);
        var candidate = rule.StartDate.AddDays(intervalsToSkip * rule.Interval);

        while (candidate <= toInclusive)
        {
            if (candidate >= fromInclusive)
            {
                occurrences.Add(candidate);
            }

            candidate = candidate.AddDays(rule.Interval);
        }

        return occurrences;
    }

    private static IReadOnlyList<DateOnly> GetWeeklyOccurrences(
        RecurrenceRule rule,
        DateOnly fromInclusive,
        DateOnly toInclusive)
    {
        var occurrences = new List<DateOnly>();
        var targetDay = rule.DayOfWeek ?? rule.StartDate.DayOfWeek;
        var first = NextOrSameDayOfWeek(rule.StartDate, targetDay);
        var intervalDays = rule.Interval * 7;
        var daysFromFirst = Math.Max(0, fromInclusive.DayNumber - first.DayNumber);
        var intervalsToSkip = CeilingDivide(daysFromFirst, intervalDays);
        var candidate = first.AddDays(intervalsToSkip * intervalDays);

        while (candidate <= toInclusive)
        {
            if (candidate >= fromInclusive && candidate >= rule.StartDate)
            {
                occurrences.Add(candidate);
            }

            candidate = candidate.AddDays(intervalDays);
        }

        return occurrences;
    }

    private static IReadOnlyList<DateOnly> GetMonthlyOccurrences(
        RecurrenceRule rule,
        DateOnly fromInclusive,
        DateOnly toInclusive)
    {
        var occurrences = new List<DateOnly>();
        var monthCursor = new DateOnly(rule.StartDate.Year, rule.StartDate.Month, 1);
        var candidate = CreateMonthlyOccurrence(rule, monthCursor);

        if (candidate < rule.StartDate)
        {
            monthCursor = monthCursor.AddMonths(rule.Interval);
            candidate = CreateMonthlyOccurrence(rule, monthCursor);
        }

        while (candidate < fromInclusive)
        {
            monthCursor = monthCursor.AddMonths(rule.Interval);
            candidate = CreateMonthlyOccurrence(rule, monthCursor);
        }

        while (candidate <= toInclusive)
        {
            if (candidate >= rule.StartDate)
            {
                occurrences.Add(candidate);
            }

            monthCursor = monthCursor.AddMonths(rule.Interval);
            candidate = CreateMonthlyOccurrence(rule, monthCursor);
        }

        return occurrences;
    }

    private static IReadOnlyList<DateOnly> GetYearlyOccurrences(
        RecurrenceRule rule,
        DateOnly fromInclusive,
        DateOnly toInclusive)
    {
        var occurrences = new List<DateOnly>();
        var year = rule.StartDate.Year;
        var candidate = CreateYearlyOccurrence(rule, year);

        if (candidate < rule.StartDate)
        {
            year += rule.Interval;
            candidate = CreateYearlyOccurrence(rule, year);
        }

        while (candidate < fromInclusive)
        {
            year += rule.Interval;
            candidate = CreateYearlyOccurrence(rule, year);
        }

        while (candidate <= toInclusive)
        {
            if (candidate >= rule.StartDate)
            {
                occurrences.Add(candidate);
            }

            year += rule.Interval;
            candidate = CreateYearlyOccurrence(rule, year);
        }

        return occurrences;
    }

    private static DateOnly CreateMonthlyOccurrence(RecurrenceRule rule, DateOnly month)
    {
        if (rule.MonthlyMode == MonthlyRecurrenceMode.LastDayOfMonth)
        {
            return new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
        }

        var requestedDay = rule.DayOfMonth ?? rule.StartDate.Day;
        var day = Math.Min(requestedDay, DateTime.DaysInMonth(month.Year, month.Month));

        return new DateOnly(month.Year, month.Month, day);
    }

    private static DateOnly CreateYearlyOccurrence(RecurrenceRule rule, int year)
    {
        var day = Math.Min(rule.StartDate.Day, DateTime.DaysInMonth(year, rule.StartDate.Month));
        return new DateOnly(year, rule.StartDate.Month, day);
    }

    private static DateOnly NextOrSameDayOfWeek(DateOnly date, DayOfWeek dayOfWeek)
    {
        var daysToAdd = ((int)dayOfWeek - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(daysToAdd);
    }

    private static DateOnly Min(DateOnly left, DateOnly right)
    {
        return left <= right ? left : right;
    }

    private static int CeilingDivide(int value, int divisor)
    {
        if (value <= 0)
        {
            return 0;
        }

        return (value + divisor - 1) / divisor;
    }

    private static void Validate(RecurrenceRule rule)
    {
        if (rule.Interval < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rule), "Recurrence interval must be at least 1.");
        }

        if (rule.DayOfMonth is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(rule), "Day of month must be between 1 and 31.");
        }
    }
}
