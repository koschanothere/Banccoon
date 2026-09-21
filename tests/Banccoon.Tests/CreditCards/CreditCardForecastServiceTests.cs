using Banccoon.Core.CreditCards;
using Banccoon.Core.Models;
using Xunit;

namespace Banccoon.Tests.CreditCards;

public sealed class CreditCardForecastServiceTests
{
    private readonly CreditCardForecastService service = new();

    [Fact]
    public void ProjectPayments_UsesPlannedPaymentBeforeMinimumPayment()
    {
        var account = CreateCreditCard(
            currentDebt: 1000m,
            dueDay: 10,
            minimumPayment: 50m,
            plannedPayment: 200m);

        var payments = service.ProjectPayments(
            new[] { account },
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 7, 31));

        Assert.Equal(2, payments.Count);
        Assert.All(payments, payment => Assert.Equal(CreditCardPaymentSource.PlannedPayment, payment.Source));
        Assert.Equal(new DateOnly(2026, 6, 10), payments[0].PaymentDate);
        Assert.Equal(200m, payments[0].Amount);
        Assert.Equal(new DateOnly(2026, 7, 10), payments[1].PaymentDate);
        Assert.Equal(200m, payments[1].Amount);
    }

    [Fact]
    public void ProjectPayments_FallsBackToMinimumPaymentAndCapsFinalPayment()
    {
        var account = CreateCreditCard(
            currentDebt: 120m,
            dueDay: 15,
            minimumPayment: 50m,
            plannedPayment: null);

        var payments = service.ProjectPayments(
            new[] { account },
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 8, 31));

        var amounts = payments.Select(payment => payment.Amount).ToArray();

        Assert.Equal(50m, amounts[0]);
        Assert.Equal(50m, amounts[1]);
        Assert.Equal(20m, amounts[2]);
        Assert.All(payments, payment => Assert.Equal(CreditCardPaymentSource.MinimumPayment, payment.Source));
    }

    [Fact]
    public void ProjectPayments_SkipsCardsWithoutEnoughPaymentInformation()
    {
        var account = CreateCreditCard(
            currentDebt: 120m,
            dueDay: null,
            minimumPayment: 50m,
            plannedPayment: null);

        var payments = service.ProjectPayments(
            new[] { account },
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 8, 31));

        Assert.Empty(payments);
    }

    [Fact]
    public void CalculatePayoffPlan_UsesChosenPaymentAmount()
    {
        var account = CreateCreditCard(
            currentDebt: 450m,
            dueDay: 15,
            minimumPayment: 50m,
            plannedPayment: null);

        var plan = service.CalculatePayoffPlan(
            account,
            paymentAmount: 200m,
            firstPaymentDate: new DateOnly(2026, 6, 15));

        Assert.True(plan.IsPaidOff);
        Assert.Equal(3, plan.MonthCount);
        Assert.Equal(450m, plan.TotalPaid);
        Assert.Equal(new DateOnly(2026, 8, 15), plan.FinalPaymentDate);
        Assert.Equal(50m, plan.Months[^1].PaymentAmount);
    }

    [Fact]
    public void CalculatePayoffPlan_ManualFinanceChargeExtendsPayoff()
    {
        var account = CreateCreditCard(
            currentDebt: 300m,
            dueDay: 15,
            minimumPayment: 50m,
            plannedPayment: null);

        var plan = service.CalculatePayoffPlan(
            account,
            paymentAmount: 100m,
            firstPaymentDate: new DateOnly(2026, 6, 15),
            manualMonthlyFinanceCharge: 10m);

        Assert.True(plan.IsPaidOff);
        Assert.Equal(4, plan.MonthCount);
        Assert.Equal(340m, plan.TotalPaid);
        Assert.Equal(40m, plan.Months[^1].PaymentAmount);
    }

    [Fact]
    public void CalculatePayoffPlan_ReturnsUnpaidWhenPaymentDoesNotReduceDebt()
    {
        var account = CreateCreditCard(
            currentDebt: 300m,
            dueDay: 15,
            minimumPayment: 50m,
            plannedPayment: null);

        var plan = service.CalculatePayoffPlan(
            account,
            paymentAmount: 10m,
            firstPaymentDate: new DateOnly(2026, 6, 15),
            manualMonthlyFinanceCharge: 10m,
            maxMonths: 3);

        Assert.False(plan.IsPaidOff);
        Assert.Equal(3, plan.MonthCount);
        Assert.Null(plan.FinalPaymentDate);
        Assert.Equal(300m, plan.Months[^1].EndingDebt);
    }

    private static Account CreateCreditCard(
        decimal currentDebt,
        int? dueDay,
        decimal? minimumPayment,
        decimal? plannedPayment)
    {
        return new Account(
            Guid.NewGuid(),
            "Everyday card",
            AccountType.CreditCard,
            0m,
            "EUR",
            DateTimeOffset.UtcNow,
            IsArchived: false,
            new CreditCardDetails(
                CurrentDebt: currentDebt,
                StatementDayOfMonth: null,
                PaymentDueDayOfMonth: dueDay,
                MinimumPayment: minimumPayment,
                PlannedPaymentAmount: plannedPayment));
    }
}
