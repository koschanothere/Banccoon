using Banccoon.App.Formatting;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class ForecastChartPointViewModel
{
    public ForecastChartPointViewModel(
        DateOnly date,
        decimal balance,
        IReadOnlyList<string> eventSummaries,
        string currency,
        DateDisplayFormat dateDisplayFormat,
        bool isCurrentDate = false,
        bool isHistorical = false)
    {
        Date = date;
        Balance = balance;
        EventSummaries = eventSummaries;
        IsCurrentDate = isCurrentDate;
        IsHistorical = isHistorical;
        DateText = DateDisplay.Format(date, dateDisplayFormat);
        ShortDateText = DateDisplay.FormatShortWithoutYear(date, dateDisplayFormat);
        BalanceText = MoneyFormat.Format(balance, currency);
        EventsText = eventSummaries.Count == 0
            ? isCurrentDate ? "Current balance" : isHistorical ? "No account changes" : "No planned events"
            : string.Join(" | ", eventSummaries);
    }

    public DateOnly Date { get; }

    public decimal Balance { get; }

    public IReadOnlyList<string> EventSummaries { get; }

    public bool IsCurrentDate { get; }

    public bool IsHistorical { get; }

    public string DateText { get; }

    public string ShortDateText { get; }

    public string BalanceText { get; }

    public string EventsText { get; }
}
