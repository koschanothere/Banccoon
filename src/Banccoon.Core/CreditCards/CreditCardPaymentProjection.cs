namespace Banccoon.Core.CreditCards;

public sealed record CreditCardPaymentProjection(
    Guid AccountId,
    string AccountName,
    DateOnly PaymentDate,
    decimal Amount,
    CreditCardPaymentSource Source);
