using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Analytics;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

public sealed class AnalyticsCategoryRowViewModel
{
    private const double MaxBarHeight = 32;
    private const double MinBarHeight = 2;

    public AnalyticsCategoryRowViewModel(AnalyticsCategoryTrend trend, Color color, string currency, Func<Guid?, Task> onSelected)
    {
        CategoryId = trend.CategoryId;
        CategoryName = trend.CategoryName ?? "Uncategorized";
        Amount = trend.CurrentPeriodTotal;
        Color = color;
        CurrentTotalText = MoneyFormat.Format(trend.CurrentPeriodTotal, currency);
        ChangeText = BuildChangeText(trend, currency);
        IsIncrease = trend.ChangeAmount > 0m;
        IsDecrease = trend.ChangeAmount < 0m;

        var maxTotal = trend.Points.Count == 0 ? 0m : trend.Points.Max(point => point.Total);
        Bars = trend.Points
            .Select((point, index) => new AnalyticsBarViewModel(
                maxTotal <= 0m ? MinBarHeight : Math.Max(MinBarHeight, (double)(point.Total / maxTotal) * MaxBarHeight),
                point.PeriodLabel,
                isCurrent: index == trend.Points.Count - 1))
            .ToList();

        DrillDownCommand = new RelayCommand(() => _ = onSelected(CategoryId));
    }

    public Guid? CategoryId { get; }

    public string CategoryName { get; }

    public decimal Amount { get; }

    public Color Color { get; }

    public string CurrentTotalText { get; }

    public string ChangeText { get; }

    public bool IsIncrease { get; }

    public bool IsDecrease { get; }

    public IReadOnlyList<AnalyticsBarViewModel> Bars { get; }

    public ICommand DrillDownCommand { get; }

    private static string BuildChangeText(AnalyticsCategoryTrend trend, string currency)
    {
        if (trend.PreviousPeriodTotal == 0m)
        {
            return trend.CurrentPeriodTotal == 0m ? "No spend" : "New this month";
        }

        var sign = trend.ChangeAmount >= 0m ? "+" : "-";
        var amountText = MoneyFormat.Format(Math.Abs(trend.ChangeAmount), currency);
        var percentText = trend.ChangePercent is { } percent ? $" ({sign}{Math.Abs(percent):P0})" : string.Empty;
        return $"{sign}{amountText}{percentText} vs last month";
    }
}
