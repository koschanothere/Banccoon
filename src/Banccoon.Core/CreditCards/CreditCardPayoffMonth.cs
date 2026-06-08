namespace Banccoon.Core.CreditCards;

public sealed record CreditCardPayoffMonth(
    int MonthNumber,
    DateOnly PaymentDate,
    decimal StartingDebt,
    decimal ManualFinanceCharge,
    decimal PaymentAmount,
    decimal EndingDebt);
