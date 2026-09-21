using Banccoon.Core.Models;

namespace Banccoon.Core.CreditCards;

public interface ICreditCardForecastService
{
    IReadOnlyList<CreditCardPaymentProjection> ProjectPayments(
        IEnumerable<Account> accounts,
        DateOnly fromInclusive,
        DateOnly toInclusive);

    CreditCardPayoffPlan CalculatePayoffPlan(
        Account account,
        decimal paymentAmount,
        DateOnly firstPaymentDate,
        decimal manualMonthlyFinanceCharge = 0m,
        int maxMonths = 600);
}
