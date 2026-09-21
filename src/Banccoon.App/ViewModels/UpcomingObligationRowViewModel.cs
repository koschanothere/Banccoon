using Banccoon.App.Formatting;
using Banccoon.Core.Forecasting;

namespace Banccoon.App.ViewModels;

public sealed class UpcomingObligationRowViewModel
{
    public UpcomingObligationRowViewModel(UpcomingObligation obligation, DateOnly today, string currency)
    {
        DateText = obligation.Date == today ? "Today" : obligation.Date.ToString("dd/MM/yyyy");
        Name = obligation.Name;
        AmountText = MoneyFormat.Format(-obligation.Amount, currency);
    }

    public string DateText { get; }

    public string Name { get; }

    public string AmountText { get; }
}
