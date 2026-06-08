using Banccoon.Core.Abstractions;
using Banccoon.Core.CreditCards;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class CreditCardDetailsViewModel : ViewModelBase
{
    private readonly ICreditCardForecastService creditCardForecastService;
    private Account? account;
    private decimal chosenPaymentAmount;
    private decimal manualMonthlyFinanceCharge;
    private DateOnly firstPaymentDate;
    private CreditCardPayoffPlan? payoffPlan;

    public CreditCardDetailsViewModel(
        ICreditCardForecastService creditCardForecastService,
        IDateProvider dateProvider)
    {
        this.creditCardForecastService = creditCardForecastService;
        firstPaymentDate = dateProvider.Today;
    }

    public Account? Account
    {
        get => account;
        set
        {
            if (SetProperty(ref account, value))
            {
                ChosenPaymentAmount = GetDefaultPaymentAmount(value);
                RecalculatePayoff();
            }
        }
    }

    public decimal ChosenPaymentAmount
    {
        get => chosenPaymentAmount;
        set
        {
            if (SetProperty(ref chosenPaymentAmount, Math.Max(0m, value)))
            {
                RecalculatePayoff();
            }
        }
    }

    public decimal ManualMonthlyFinanceCharge
    {
        get => manualMonthlyFinanceCharge;
        set
        {
            if (SetProperty(ref manualMonthlyFinanceCharge, Math.Max(0m, value)))
            {
                RecalculatePayoff();
            }
        }
    }

    public DateOnly FirstPaymentDate
    {
        get => firstPaymentDate;
        set
        {
            if (SetProperty(ref firstPaymentDate, value))
            {
                RecalculatePayoff();
            }
        }
    }

    public CreditCardPayoffPlan? PayoffPlan
    {
        get => payoffPlan;
        private set
        {
            if (SetProperty(ref payoffPlan, value))
            {
                OnPropertyChanged(nameof(PayoffSummary));
            }
        }
    }

    public string PayoffSummary
    {
        get
        {
            if (PayoffPlan is null)
            {
                return "Choose a payment amount to calculate payoff timing.";
            }

            if (!PayoffPlan.IsPaidOff)
            {
                return $"Not paid off within {PayoffPlan.MonthCount} months at this payment amount.";
            }

            return PayoffPlan.MonthCount == 1
                ? "Paid off with the next payment."
                : $"Paid off in {PayoffPlan.MonthCount} months.";
        }
    }

    private void RecalculatePayoff()
    {
        if (Account is null || Account.Type != AccountType.CreditCard || ChosenPaymentAmount <= 0m)
        {
            PayoffPlan = null;
            return;
        }

        PayoffPlan = creditCardForecastService.CalculatePayoffPlan(
            Account,
            ChosenPaymentAmount,
            FirstPaymentDate,
            ManualMonthlyFinanceCharge);
    }

    private static decimal GetDefaultPaymentAmount(Account? account)
    {
        if (account?.CreditCardDetails?.PlannedPaymentAmount is { } plannedPayment && plannedPayment > 0m)
        {
            return plannedPayment;
        }

        if (account?.CreditCardDetails?.MinimumPayment is { } minimumPayment && minimumPayment > 0m)
        {
            return minimumPayment;
        }

        return 0m;
    }
}
