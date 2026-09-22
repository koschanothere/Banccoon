using Banccoon.App.Localization;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.App.Formatting;

// One line per event in the balance chart's callout ("Groceries: EUR -30.00"), for both halves of
// the chart: forecast points (scheduled ForecastEvents) and historical points (recorded
// transactions, via HistoricalBalanceEvent). Kept in one place so both read the same way.
public static class ChartEventSummaryFormat
{
    public static string Format(ForecastEvent forecastEvent, string currency)
    {
        return $"{forecastEvent.Name}: {MoneyFormat.Format(forecastEvent.SignedAmount, currency)}";
    }

    public static string Format(HistoricalBalanceEvent historicalEvent, string currency)
    {
        // Transaction.Name can be blank on older data (it was added after the model); fall back to
        // the translated type label rather than showing ": EUR -30.00" with no name at all.
        var name = string.IsNullOrWhiteSpace(historicalEvent.Name)
            ? DisplayText.Format(historicalEvent.Type)
            : historicalEvent.Name;

        // A transfer between two included accounts doesn't move the total (TotalEffect is 0), so
        // show the amount that moved instead of a meaningless "EUR 0.00".
        return historicalEvent.Type == TransactionType.Transfer && historicalEvent.TotalEffect == 0m
            ? string.Format(
                Translator.Get("ForecastChart_InternalTransferFormat"),
                name,
                MoneyFormat.Format(historicalEvent.Amount, currency))
            : $"{name}: {MoneyFormat.Format(historicalEvent.TotalEffect, currency)}";
    }
}
