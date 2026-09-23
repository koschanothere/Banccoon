using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Recurrence;
static partial class Checks
{
    public static void RunRecurrenceDescriptions()
    {
        var describe = new RecurrenceDescriptionService();
        string New(RecurrenceRule r) => RecurrenceDescriptionFormatter.Format(describe.Describe(r));
        var start = new DateOnly(2026, 6, 7);

        // English keeps the pre-translation wording (verified byte-identical to the old hardcoded
        // formatter across 3,120 rules when the translation landed - see docs/development-phases.md).
        Translator.SetLanguage("en");
        Check("en weekly", New(new(RecurrenceFrequency.Weekly, 2, start, DayOfWeek: DayOfWeek.Monday)), "Every 2 weeks on Monday");
        Check("en monthly last", New(new(RecurrenceFrequency.Monthly, 1, start, MonthlyMode: MonthlyRecurrenceMode.LastDayOfMonth)), "Every month on the last day");
        Check("en yearly until", New(new(RecurrenceFrequency.Yearly, 1, start, new DateOnly(2027, 1, 31))), "Every year on June 7 until January 31, 2027");

        // Russian: exact expected sentences for each grammatical branch.
        Translator.SetLanguage("ru");
        var until = new DateOnly(2026, 12, 14);
        Check("ru daily 1", New(new(RecurrenceFrequency.Daily, 1, start)), "Каждый день");
        Check("ru daily 2", New(new(RecurrenceFrequency.Daily, 2, start)), "Каждые 2 дня");
        Check("ru daily 5", New(new(RecurrenceFrequency.Daily, 5, start)), "Каждые 5 дней");
        Check("ru daily 11", New(new(RecurrenceFrequency.Daily, 11, start)), "Каждые 11 дней");
        Check("ru daily 21", New(new(RecurrenceFrequency.Daily, 21, start)), "Каждый 21 день");
        Check("ru daily 22", New(new(RecurrenceFrequency.Daily, 22, start)), "Каждые 22 дня");
        Check("ru weekly 1 mon", New(new(RecurrenceFrequency.Weekly, 1, start, DayOfWeek: DayOfWeek.Monday)), "Каждую неделю по понедельникам");
        Check("ru weekly 2 wed", New(new(RecurrenceFrequency.Weekly, 2, start, DayOfWeek: DayOfWeek.Wednesday)), "Каждые 2 недели по средам");
        Check("ru weekly 5 fri", New(new(RecurrenceFrequency.Weekly, 5, start, DayOfWeek: DayOfWeek.Friday)), "Каждые 5 недель по пятницам");
        Check("ru weekly 21 sun", New(new(RecurrenceFrequency.Weekly, 21, start, DayOfWeek: DayOfWeek.Sunday)), "Каждую 21 неделю по воскресеньям");
        Check("ru weekly tue", New(new(RecurrenceFrequency.Weekly, 1, start, DayOfWeek: DayOfWeek.Tuesday)), "Каждую неделю по вторникам");
        Check("ru weekly thu", New(new(RecurrenceFrequency.Weekly, 1, start, DayOfWeek: DayOfWeek.Thursday)), "Каждую неделю по четвергам");
        Check("ru weekly sat", New(new(RecurrenceFrequency.Weekly, 1, start, DayOfWeek: DayOfWeek.Saturday)), "Каждую неделю по субботам");
        Check("ru monthly 1 d25", New(new(RecurrenceFrequency.Monthly, 1, start, DayOfMonth: 25)), "Каждый месяц, 25 числа");
        Check("ru monthly 3 d1", New(new(RecurrenceFrequency.Monthly, 3, start, DayOfMonth: 1)), "Каждые 3 месяца, 1 числа");
        Check("ru monthly 6 d10", New(new(RecurrenceFrequency.Monthly, 6, start, DayOfMonth: 10)), "Каждые 6 месяцев, 10 числа");
        Check("ru monthly last", New(new(RecurrenceFrequency.Monthly, 1, start, MonthlyMode: MonthlyRecurrenceMode.LastDayOfMonth)), "Каждый месяц, в последний день");
        Check("ru monthly 2 last", New(new(RecurrenceFrequency.Monthly, 2, start, MonthlyMode: MonthlyRecurrenceMode.LastDayOfMonth)), "Каждые 2 месяца, в последний день");
        Check("ru yearly 1", New(new(RecurrenceFrequency.Yearly, 1, start)), "Каждый год, 7 июня");
        Check("ru yearly 2", New(new(RecurrenceFrequency.Yearly, 2, start)), "Каждые 2 года, 7 июня");
        Check("ru yearly 5", New(new(RecurrenceFrequency.Yearly, 5, new DateOnly(2026, 3, 8))), "Каждые 5 лет, 8 марта");
        Check("ru yearly 21", New(new(RecurrenceFrequency.Yearly, 21, new DateOnly(2026, 1, 1))), "Каждый 21 год, 1 января");
        Check("ru daily until", New(new(RecurrenceFrequency.Daily, 1, start, until)), "Каждый день, до 14 декабря 2026 г.");
        Check("ru weekly until", New(new(RecurrenceFrequency.Weekly, 2, start, until, DayOfWeek: DayOfWeek.Monday)), "Каждые 2 недели по понедельникам, до 14 декабря 2026 г.");
        Check("ru monthly until", New(new(RecurrenceFrequency.Monthly, 1, start, until, DayOfMonth: 25)), "Каждый месяц, 25 числа, до 14 декабря 2026 г.");
        string[] genitive = ["января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря"];
        for (var m = 1; m <= 12; m++)
            Check($"ru yearly month {m}", New(new(RecurrenceFrequency.Yearly, 1, new DateOnly(2026, m, 3))), $"Каждый год, 3 {genitive[m - 1]}");

        // Switching back must restore English (language state, not a leaked culture).
        Translator.SetLanguage("en");
        Check("en after ru", New(new(RecurrenceFrequency.Yearly, 2, start, until)), "Every 2 years on June 7 until December 14, 2026");
    }
}
