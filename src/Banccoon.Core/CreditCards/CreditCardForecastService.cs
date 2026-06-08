using Banccoon.Core.Models;

namespace Banccoon.Core.CreditCards;

public sealed class CreditCardForecastService : ICreditCardForecastService
{
    public IReadOnlyList<CreditCardPaymentProjection> ProjectPayments(
        IEnumerable<Account> accounts,
        DateOnly fromInclusive,
        DateOnly toInclusive)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        if (toInclusive < fromInclusive)
        {
            throw new ArgumentException("Projection end date must be on or after start date.", nameof(toInclusive));
        }

        var projections = new List<CreditCardPaymentProjection>();

        foreach (var account in accounts.Where(account => account.Type == AccountType.CreditCard && !account.IsArchived))
        {
            var details = account.CreditCardDetails;
            if (details is null)
            {
                continue;
            }

            var remainingDebt = GetEffectiveDebt(account);
            var paymentDueDay = details.PaymentDueDayOfMonth;
            var paymentOption = GetForecastPaymentOption(details);
            if (remainingDebt <= 0m || paymentDueDay is null || paymentOption.Amount <= 0m)
            {
                continue;
            }

            var monthCursor = new DateOnly(fromInclusive.Year, fromInclusive.Month, 1);
            while (true)
            {
                var paymentDate = GetClampedMonthDate(monthCursor.Year, monthCursor.Month, paymentDueDay.Value);
                if (paymentDate is null)
                {
                    break;
                }

                if (paymentDate.Value < fromInclusive)
                {
                    monthCursor = monthCursor.AddMonths(1);
                    continue;
                }

                if (paymentDate.Value > toInclusive)
                {
                    break;
                }

                var amount = Math.Min(paymentOption.Amount, remainingDebt);
                projections.Add(new CreditCardPaymentProjection(
                    account.Id,
                    account.Name,
                    paymentDate.Value,
                    amount,
                    paymentOption.Source));

                remainingDebt -= amount;
                if (remainingDebt <= 0m)
                {
                    break;
                }

                monthCursor = monthCursor.AddMonths(1);
            }
        }

        return projections
            .OrderBy(payment => payment.PaymentDate)
            .ThenBy(payment => payment.Amount)
            .ThenBy(payment => payment.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public CreditCardPayoffPlan CalculatePayoffPlan(
        Account account,
        decimal paymentAmount,
        DateOnly firstPaymentDate,
        decimal manualMonthlyFinanceCharge = 0m,
        int maxMonths = 600)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (account.Type != AccountType.CreditCard)
        {
            throw new ArgumentException("Payoff plans can only be calculated for credit card accounts.", nameof(account));
        }

        var normalizedPayment = Math.Max(0m, paymentAmount);
        if (normalizedPayment <= 0m)
        {
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(paymentAmount));
        }

        if (maxMonths < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMonths), "Maximum months must be at least one.");
        }

        var startingDebt = GetEffectiveDebt(account);
        var financeCharge = Math.Max(0m, manualMonthlyFinanceCharge);
        var months = new List<CreditCardPayoffMonth>();
        var debt = startingDebt;
        var totalPaid = 0m;
        DateOnly? finalPaymentDate = null;

        if (debt <= 0m)
        {
            return new CreditCardPayoffPlan(
                account.Id,
                account.Name,
                startingDebt,
                normalizedPayment,
                financeCharge,
                firstPaymentDate,
                IsPaidOff: true,
                MonthCount: 0,
                TotalPaid: 0m,
                FinalPaymentDate: null,
                Months: Array.Empty<CreditCardPayoffMonth>());
        }

        for (var monthNumber = 1; monthNumber <= maxMonths; monthNumber++)
        {
            var paymentDate = firstPaymentDate.AddMonths(monthNumber - 1);
            var debtBeforePayment = debt + financeCharge;
            var actualPayment = Math.Min(normalizedPayment, debtBeforePayment);
            var endingDebt = Math.Max(0m, debtBeforePayment - actualPayment);

            months.Add(new CreditCardPayoffMonth(
                monthNumber,
                paymentDate,
                debt,
                financeCharge,
                actualPayment,
                endingDebt));

            totalPaid += actualPayment;
            debt = endingDebt;

            if (debt <= 0m)
            {
                finalPaymentDate = paymentDate;
                break;
            }
        }

        return new CreditCardPayoffPlan(
            account.Id,
            account.Name,
            startingDebt,
            normalizedPayment,
            financeCharge,
            firstPaymentDate,
            debt <= 0m,
            months.Count,
            totalPaid,
            finalPaymentDate,
            months);
    }

    private static decimal GetEffectiveDebt(Account account)
    {
        if (account.CreditCardDetails?.CurrentDebt is { } currentDebt)
        {
            return Math.Max(0m, currentDebt);
        }

        return account.CurrentBalance < 0m
            ? Math.Abs(account.CurrentBalance)
            : 0m;
    }

    private static (decimal Amount, CreditCardPaymentSource Source) GetForecastPaymentOption(CreditCardDetails details)
    {
        if (details.PlannedPaymentAmount is { } plannedPayment && plannedPayment > 0m)
        {
            return (plannedPayment, CreditCardPaymentSource.PlannedPayment);
        }

        if (details.MinimumPayment is { } minimumPayment && minimumPayment > 0m)
        {
            return (minimumPayment, CreditCardPaymentSource.MinimumPayment);
        }

        return (0m, CreditCardPaymentSource.CustomPayment);
    }

    private static DateOnly? GetClampedMonthDate(int year, int month, int dayOfMonth)
    {
        if (dayOfMonth is < 1 or > 31)
        {
            return null;
        }

        var clampedDay = Math.Min(dayOfMonth, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, clampedDay);
    }
}
