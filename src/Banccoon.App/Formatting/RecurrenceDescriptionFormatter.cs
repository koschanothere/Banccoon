using System.Globalization;
using Banccoon.App.Localization;
using Banccoon.Core.Recurrence;

namespace Banccoon.App.Formatting;

// Turns Core's language-agnostic RecurrenceDescriptionData into a translated sentence
// ("Every 2 weeks on Monday" / "Каждые 2 недели по понедельникам").
//
// Built from whole-phrase resx keys rather than slotting translated words into one shared
// template, because Russian inflects every slot differently by position:
// - The "every N units" phrase is pluralized per language (Translator.GetPlural), and in Russian
//   "каждый" agrees with the numeral's noun form too: "каждый 21 день" / "каждые 2 дня" /
//   "каждые 5 дней", and "каждую неделю" (feminine accusative) vs "каждый месяц".
// - Weekdays are whole "on Monday" / "по понедельникам" phrases (по + dative plural for a
//   recurring day), one key per day - a bare day name can't be declined by a template.
// - Dates use a per-language pattern from resx ("MMMM d" / "d MMMM") formatted with the UI
//   culture: .NET picks the genitive month form ("7 июня", not "7 июнь") whenever the pattern
//   puts a day number next to MMMM, so the month's case comes from CLDR culture data rather than
//   a hand-maintained list.
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
            ? string.Format(
                Translator.Get("RecurrenceDescription_UntilFormat"),
                coreDescription,
                FormatDate(data.EndDate.Value, "RecurrenceDescription_UntilDatePattern"))
            : coreDescription;
    }

    private static string DescribeDaily(RecurrenceDescriptionData data)
    {
        return Every(data.Interval, "RecurrenceDescription_EveryDay", "RecurrenceDescription_EveryNDays");
    }

    private static string DescribeWeekly(RecurrenceDescriptionData data)
    {
        return string.Format(
            Translator.Get("RecurrenceDescription_WeeklyFormat"),
            Every(data.Interval, "RecurrenceDescription_EveryWeek", "RecurrenceDescription_EveryNWeeks"),
            Translator.Get($"RecurrenceDescription_OnWeekday_{data.ResolvedDayOfWeek!.Value}"));
    }

    private static string DescribeMonthly(RecurrenceDescriptionData data)
    {
        var every = Every(data.Interval, "RecurrenceDescription_EveryMonth", "RecurrenceDescription_EveryNMonths");

        return data.IsLastDayOfMonth
            ? string.Format(Translator.Get("RecurrenceDescription_MonthlyOnLastDayFormat"), every)
            : string.Format(Translator.Get("RecurrenceDescription_MonthlyOnDayFormat"), every, data.ResolvedDayOfMonth!.Value);
    }

    private static string DescribeYearly(RecurrenceDescriptionData data)
    {
        return string.Format(
            Translator.Get("RecurrenceDescription_YearlyFormat"),
            Every(data.Interval, "RecurrenceDescription_EveryYear", "RecurrenceDescription_EveryNYears"),
            FormatDate(data.StartDate, "RecurrenceDescription_YearlyDatePattern"));
    }

    // Interval 1 gets its own key ("Every week" / "Каждую неделю") rather than going through the
    // plural path, which would read "Every 1 week" / "Каждую 1 неделю".
    private static string Every(int interval, string singleKey, string pluralKeyBase)
    {
        return interval == 1
            ? Translator.Get(singleKey)
            : Translator.GetPlural(pluralKeyBase, interval);
    }

    private static string FormatDate(DateOnly date, string patternKey)
    {
        return date.ToString(Translator.Get(patternKey), CultureInfo.CurrentUICulture);
    }
}
