using Banccoon.Core.CreditCards;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Savings;
using Xunit;

namespace Banccoon.Tests.Forecasting;

public sealed class ForecastServicePhase6Tests
{
    private readonly ForecastService service = new(
        new AccountBalanceService(),
        new ScheduledTransactionProjectionService(new RecurrenceService()),
        new CreditCardForecastService(),
        new SavingsGoalAllocationService());

    [Fact]
    public void CreateForecast_IncludesCreditCardPlannedPaymentAsObligation()
    {
        var checking = CreateDebitAccount(1000m);
        var creditCard = CreateCreditCard(
            currentDebt: 300m,
            dueDay: 15,
            minimumPayment: 50m,
            plannedPayment: 120m);

        var result = service.CreateForecast(new ForecastRequest(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            new[] { checking, creditCard },
            Array.Empty<ScheduledTransaction>()));

        var cardPayment = Assert.Single(result.Events, forecastEvent => forecastEvent.Kind == ForecastEventKind.CreditCardPayment);

        Assert.Equal(new DateOnly(2026, 6, 15), cardPayment.Date);
        Assert.Equal(120m, cardPayment.Amount);
        Assert.Equal(880m, result.ForecastedBalance);
        Assert.Contains(result.UpcomingObligations, obligation => obligation.Kind == ForecastEventKind.CreditCardPayment);
    }

    [Fact]
    public void CreateForecast_SavingsGoalsReduceAvailableToSpendWithoutChangingForecastBalance()
    {
        var checking = CreateDebitAccount(1000m);
        var goal = new SavingsGoal(
            Guid.NewGuid(),
            "Emergency fund",
            1000m,
            400m,
            TargetDate: null);

        var result = service.CreateForecast(new ForecastRequest(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            new[] { checking },
            Array.Empty<ScheduledTransaction>(),
            new[] { goal }));

        Assert.Equal(1000m, result.ForecastedBalance);
        Assert.Equal(1000m, result.LowestForecastedBalance);
        Assert.Equal(600m, result.AvailableToSpend);
    }

    private static Account CreateDebitAccount(decimal balance)
    {
        return new Account(
            Guid.NewGuid(),
            "Checking",
            AccountType.DebitCard,
            balance,
            "EUR",
            DateTimeOffset.UtcNow);
    }

    private static Account CreateCreditCard(
        decimal currentDebt,
        int dueDay,
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
