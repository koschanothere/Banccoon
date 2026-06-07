using Banccoon.Core.Abstractions;

namespace Banccoon.App.ViewModels;

public sealed class DashboardViewModel
{
    public DashboardViewModel(IDateProvider dateProvider)
    {
        Today = dateProvider.Today;
    }

    public DateOnly Today { get; }

    public string Title => "Banccoon";

    public string Subtitle => "Forecast-first private finance";
}
