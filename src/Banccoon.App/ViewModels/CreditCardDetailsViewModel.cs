using System.Globalization;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Abstractions;
using Banccoon.Core.CreditCards;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class CreditCardDetailsViewModel : ViewModelBase
{
    private readonly ICreditCardForecastService creditCardForecastService;
    private bool isOpen;
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

        CloseCommand = new RelayCommand(Close);
    }

    public bool IsOpen
    {
        get => isOpen;
        private set => SetProperty(ref isOpen, value);
    }

    public Account? Account
    {
        get => account;
        private set
        {
            if (SetProperty(ref account, value))
            {
                OnPropertyChanged(nameof(AccountName));
                OnPropertyChanged(nameof(CurrentDebtText));
                OnPropertyChanged(nameof(MinimumPaymentDisplayText));
                ChosenPaymentAmount = GetDefaultPaymentAmount(value);
                RecalculatePayoff();
            }
        }
    }

    public string AccountName => Account?.Name ?? string.Empty;

    public string CurrentDebtText => Account?.CreditCardDetails?.CurrentDebt is { } debt
        ? MoneyFormat.Format(debt, Account.Currency)
        : "Not set";

    public string MinimumPaymentDisplayText => Account?.CreditCardDetails?.MinimumPayment is { } minimum
        ? MoneyFormat.Format(minimum, Account.Currency)
        : "Not set";

    public decimal ChosenPaymentAmount
    {
        get => chosenPaymentAmount;
        set
        {
            if (SetProperty(ref chosenPaymentAmount, Math.Max(0m, value)))
            {
                OnPropertyChanged(nameof(ChosenPaymentAmountText));
                RecalculatePayoff();
            }
        }
    }

    public string ChosenPaymentAmountText
    {
        get => chosenPaymentAmount.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                ChosenPaymentAmount = parsed;
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
                OnPropertyChanged(nameof(ManualMonthlyFinanceChargeText));
                RecalculatePayoff();
            }
        }
    }

    public string ManualMonthlyFinanceChargeText
    {
        get => manualMonthlyFinanceCharge.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                ManualMonthlyFinanceCharge = parsed;
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
                OnPropertyChanged(nameof(FirstPaymentDateTime));
                RecalculatePayoff();
            }
        }
    }

    public DateTime FirstPaymentDateTime
    {
        get => firstPaymentDate.ToDateTime(TimeOnly.MinValue);
        set => FirstPaymentDate = DateOnly.FromDateTime(value);
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

    public ICommand CloseCommand { get; }

    public void Open(Account selectedAccount)
    {
        Account = selectedAccount;
        IsOpen = true;
    }

    public void Close() => IsOpen = false;

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
