using System.Globalization;

namespace Banccoon.Core.Recurrence;

public sealed class RecurrenceSyntaxService : IRecurrenceSyntaxService
{
    private const string DateFormat = "yyyy-MM-dd";
    private readonly IRecurrenceValidationService recurrenceValidationService;

    public RecurrenceSyntaxService()
        : this(new RecurrenceValidationService())
    {
    }

    public RecurrenceSyntaxService(IRecurrenceValidationService recurrenceValidationService)
    {
        this.recurrenceValidationService = recurrenceValidationService;
    }

    public string Format(RecurrenceRule rule)
    {
        recurrenceValidationService.ThrowIfInvalid(rule);

        var parts = new List<string>
        {
            $"FREQ={FormatFrequency(rule.Frequency)}",
            $"INTERVAL={rule.Interval}",
            $"START={FormatDate(rule.StartDate)}"
        };

        if (rule.EndDate.HasValue)
        {
            parts.Add($"UNTIL={FormatDate(rule.EndDate.Value)}");
        }

        if (rule.Frequency == RecurrenceFrequency.Weekly)
        {
            parts.Add($"BYDAY={FormatDayOfWeek(rule.DayOfWeek ?? rule.StartDate.DayOfWeek)}");
        }

        if (rule.Frequency == RecurrenceFrequency.Monthly)
        {
            if (rule.MonthlyMode == MonthlyRecurrenceMode.LastDayOfMonth)
            {
                parts.Add("BYMONTHDAY=LAST");
            }
            else
            {
                parts.Add($"BYMONTHDAY={rule.DayOfMonth ?? rule.StartDate.Day}");
            }
        }

        return string.Join(';', parts);
    }

    public RecurrenceSyntaxParseResult TryParse(string syntax)
    {
        if (string.IsNullOrWhiteSpace(syntax))
        {
            return RecurrenceSyntaxParseResult.Failure(["Syntax is empty."]);
        }

        var errors = new List<string>();
        var fields = ParseFields(syntax, errors);

        var frequency = ReadRequired(fields, "FREQ", errors, ParseFrequency);
        var interval = ReadOptionalInt(fields, "INTERVAL", 1, errors);
        var startDate = ReadRequired(fields, "START", errors, ParseDate);
        var endDate = ReadOptional(fields, "UNTIL", errors, ParseDate);
        var dayOfWeek = ReadOptional(fields, "BYDAY", errors, ParseDayOfWeek);
        var monthlyMode = MonthlyRecurrenceMode.DayOfMonth;
        int? dayOfMonth = null;

        if (fields.TryGetValue("BYMONTHDAY", out var monthDayText))
        {
            if (string.Equals(monthDayText, "LAST", StringComparison.OrdinalIgnoreCase))
            {
                monthlyMode = MonthlyRecurrenceMode.LastDayOfMonth;
            }
            else if (int.TryParse(monthDayText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDay))
            {
                dayOfMonth = parsedDay;
            }
            else
            {
                errors.Add("BYMONTHDAY must be a number between 1 and 31, or LAST.");
            }
        }

        if (frequency is not null && startDate.HasValue)
        {
            var rule = new RecurrenceRule(
                frequency.Value,
                interval,
                startDate.Value,
                endDate,
                dayOfWeek,
                dayOfMonth,
                monthlyMode);

            var validationResult = recurrenceValidationService.Validate(rule);
            errors.AddRange(validationResult.Errors);

            return errors.Count == 0
                ? RecurrenceSyntaxParseResult.Success(rule)
                : RecurrenceSyntaxParseResult.Failure(errors);
        }

        return RecurrenceSyntaxParseResult.Failure(errors);
    }

    public IReadOnlyList<RecurrenceSyntaxExample> GetExamples()
    {
        return
        [
            new("Every day", "FREQ=DAILY;INTERVAL=1;START=2026-06-07"),
            new("Every 2 weeks on Monday", "FREQ=WEEKLY;INTERVAL=2;BYDAY=MO;START=2026-06-07"),
            new("Monthly on day 25", "FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=25;START=2026-06-07"),
            new("Last day of every month", "FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=LAST;START=2026-06-07"),
            new("Every year", "FREQ=YEARLY;INTERVAL=1;START=2026-06-07")
        ];
    }

    private static Dictionary<string, string> ParseFields(string syntax, List<string> errors)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = syntax.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == part.Length - 1)
            {
                errors.Add($"Invalid field '{part}'. Use KEY=VALUE.");
                continue;
            }

            var key = part[..separatorIndex].Trim().ToUpperInvariant();
            var value = part[(separatorIndex + 1)..].Trim();
            fields[key] = value;
        }

        return fields;
    }

    private static T? ReadRequired<T>(
        IReadOnlyDictionary<string, string> fields,
        string key,
        List<string> errors,
        Func<string, T?> parser)
        where T : struct
    {
        if (!fields.TryGetValue(key, out var value))
        {
            errors.Add($"{key} is required.");
            return null;
        }

        var parsed = parser(value);
        if (!parsed.HasValue)
        {
            errors.Add($"{key} has an unsupported value.");
        }

        return parsed;
    }

    private static T? ReadOptional<T>(
        IReadOnlyDictionary<string, string> fields,
        string key,
        List<string> errors,
        Func<string, T?> parser)
        where T : struct
    {
        if (!fields.TryGetValue(key, out var value))
        {
            return null;
        }

        var parsed = parser(value);
        if (!parsed.HasValue)
        {
            errors.Add($"{key} has an unsupported value.");
        }

        return parsed;
    }

    private static int ReadOptionalInt(
        IReadOnlyDictionary<string, string> fields,
        string key,
        int defaultValue,
        List<string> errors)
    {
        if (!fields.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{key} must be a whole number.");
        return defaultValue;
    }

    private static RecurrenceFrequency? ParseFrequency(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "DAILY" => RecurrenceFrequency.Daily,
            "WEEKLY" => RecurrenceFrequency.Weekly,
            "MONTHLY" => RecurrenceFrequency.Monthly,
            "YEARLY" => RecurrenceFrequency.Yearly,
            _ => null
        };
    }

    private static DateOnly? ParseDate(string value)
    {
        return DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static DayOfWeek? ParseDayOfWeek(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "SU" => DayOfWeek.Sunday,
            "MO" => DayOfWeek.Monday,
            "TU" => DayOfWeek.Tuesday,
            "WE" => DayOfWeek.Wednesday,
            "TH" => DayOfWeek.Thursday,
            "FR" => DayOfWeek.Friday,
            "SA" => DayOfWeek.Saturday,
            _ => null
        };
    }

    private static string FormatFrequency(RecurrenceFrequency frequency)
    {
        return frequency.ToString().ToUpperInvariant();
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    private static string FormatDayOfWeek(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Sunday => "SU",
            DayOfWeek.Monday => "MO",
            DayOfWeek.Tuesday => "TU",
            DayOfWeek.Wednesday => "WE",
            DayOfWeek.Thursday => "TH",
            DayOfWeek.Friday => "FR",
            DayOfWeek.Saturday => "SA",
            _ => throw new NotSupportedException($"Unsupported day of week: {dayOfWeek}")
        };
    }
}
