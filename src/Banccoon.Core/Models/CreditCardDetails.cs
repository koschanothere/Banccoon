namespace Banccoon.Core.Models;

public sealed record CreditCardDetails(
    decimal? CurrentDebt,
    int? StatementDayOfMonth,
    int? PaymentDueDayOfMonth,
    decimal? MinimumPayment,
    decimal? PlannedPaymentAmount);
