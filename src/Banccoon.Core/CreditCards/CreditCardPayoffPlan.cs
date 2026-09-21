namespace Banccoon.Core.CreditCards;

public sealed record CreditCardPayoffPlan(
    Guid AccountId,
    string AccountName,
    decimal StartingDebt,
    decimal PaymentAmount,
    decimal ManualMonthlyFinanceCharge,
    DateOnly FirstPaymentDate,
    bool IsPaidOff,
    int MonthCount,
    decimal TotalPaid,
    DateOnly? FinalPaymentDate,
    IReadOnlyList<CreditCardPayoffMonth> Months);
