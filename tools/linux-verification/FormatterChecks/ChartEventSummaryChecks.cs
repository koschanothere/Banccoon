using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
static partial class Checks
{
    public static void RunChartEventSummaries()
    {
        var id = Guid.NewGuid();
        foreach (var (lang, transferSuffix, expenseLabel) in new[] { ("en", "(transfer)", "Expense"), ("ru", "(перевод)", "Расход") })
        {
            Translator.SetLanguage(lang);
            Check($"{lang} expense", ChartEventSummaryFormat.Format(new HistoricalBalanceEvent(id, "Groceries", TransactionType.Expense, 30m, -30m), "rub"), "Groceries: RUB -30.00");
            Check($"{lang} income", ChartEventSummaryFormat.Format(new HistoricalBalanceEvent(id, "Salary", TransactionType.Income, 1500m, 1500m), "RUB"), "Salary: RUB 1,500.00");
            Check($"{lang} internal transfer", ChartEventSummaryFormat.Format(new HistoricalBalanceEvent(id, "To savings", TransactionType.Transfer, 50m, 0m), "RUB"), $"To savings: RUB 50.00 {transferSuffix}");
            Check($"{lang} transfer out of total", ChartEventSummaryFormat.Format(new HistoricalBalanceEvent(id, "To broker", TransactionType.Transfer, 50m, -50m), "RUB"), "To broker: RUB -50.00");
            Check($"{lang} blank name", ChartEventSummaryFormat.Format(new HistoricalBalanceEvent(id, " ", TransactionType.Expense, 5m, -5m), "RUB"), $"{expenseLabel}: RUB -5.00");
            Check($"{lang} forecast unchanged shape", ChartEventSummaryFormat.Format(new ForecastEvent(id, new DateOnly(2026, 1, 1), "Rent", 700m, TransactionType.Expense, id, null, ForecastEventKind.ScheduledTransaction), "RUB"), "Rent: RUB -700.00");
            Check($"{lang} placeholder key", Translator.Get("ForecastChart_NoAccountChanges") == "ForecastChart_NoAccountChanges" ? "missing" : "present", "present");
        }
    }
}
