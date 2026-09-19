namespace Banccoon.App.ViewModels;

// One bar in a category's mini trend sparkline. Height is already resolved to a display pixel
// value (normalized against that category's own max month) rather than a raw amount, so the XAML
// can bind it straight to HeightRequest with no converter.
public sealed class AnalyticsBarViewModel
{
    public AnalyticsBarViewModel(double height, string periodLabel, bool isCurrent)
    {
        Height = height;
        PeriodLabel = periodLabel;
        IsCurrent = isCurrent;
    }

    public double Height { get; }

    public string PeriodLabel { get; }

    public bool IsCurrent { get; }
}
