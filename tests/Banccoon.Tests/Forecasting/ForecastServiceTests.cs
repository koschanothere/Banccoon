using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Xunit;

namespace Banccoon.Tests.Forecasting;

public sealed class ForecastServiceTests
{
    private readonly ForecastService service = new(
        new AccountBalanceService(),
        new ScheduledTransactionProjectionService(new RecurrenceService()));

    [Fact]
    public void CreateForecast_IncludesScheduledIncomeAndExpenses()
    {
        var account = CreateAccount(1000m);
        var salary = CreateOneTimeScheduledTransaction(
            "Salary",
            500m,
            TransactionType.Income,
            new DateOnly(2026, 6, 10),
            account.Id);
        var rent = CreateOneTimeScheduledTransaction(
            "Rent",
            300m,
            TransactionType.Expense,
            new DateOnly(2026, 6, 12),
            account.Id);

        var result = service.CreateForecast(new ForecastRequest(
            new DateOnly(2026, 6, 7),
            new DateOnly(2026, 6, 30),
            new[] { account },
            new[] { salary, rent }));

        Assert.Equal(1000m, result.CurrentBalance);
        Assert.Equal(1200m, result.ForecastedBalance);
        Assert.Equal(2, result.Events.Count);
        Assert.Single(result.UpcomingObligations);
        Assert.Equal(300m, result.UpcomingObligations[0].Amount);
    }

    [Fact]
    public void CreateForecast_AppliesSameDayExpensesBeforeIncome_ForConservativeLowestBalance()
    {
        var account = CreateAccount(100m);
        var income = CreateOneTimeScheduledTransaction(
            "Small income",
            50m,
            TransactionType.Income,
            new DateOnly(2026, 6, 10),
            account.Id);
        var expense = CreateOneTimeScheduledTransaction(
            "Large bill",
            120m,
            TransactionType.Expense,
            new DateOnly(2026, 6, 10),
            account.Id);

        var result = service.CreateForecast(new ForecastRequest(
            new DateOnly(2026, 6, 7),
            new DateOnly(2026, 6, 30),
            new[] { account },
            new[] { income, expense }));

        Assert.Equal(30m, result.ForecastedBalance);
        Assert.Equal(-20m, result.LowestForecastedBalance);
        Assert.Equal(0m, result.AvailableToSpend);
    }

    [Fact]
    public void CreateForecast_CalculatesLowestBalanceAndAvailableToSpend()
    {
        var account = CreateAccount(1000m);
        var bill = CreateOneTimeScheduledTransaction(
            "Insurance",
            800m,
            TransactionType.Expense,
            new DateOnly(2026, 6, 8),
            account.Id);
        var income = CreateOneTimeScheduledTransaction(
            "Invoice payment",
            500m,
            TransactionType.Income,
            new DateOnly(2026, 6, 20),
            account.Id);

        var result = service.CreateForecast(new ForecastRequest(
            new DateOnly(2026, 6, 7),
            new DateOnly(2026, 6, 30),
            new[] { account },
            new[] { bill, income }));

        Assert.Equal(700m, result.ForecastedBalance);
        Assert.Equal(200m, result.LowestForecastedBalance);
        Assert.Equal(200m, result.AvailableToSpend);
    }

    [Fact]
    public void ForPeriod_CreatesInclusiveDateRange()
    {
        var request = ForecastRequest.ForPeriod(
            new DateOnly(2026, 6, 7),
            ForecastPeriod.SevenDays,
            Array.Empty<Account>(),
            Array.Empty<ScheduledTransaction>());

        Assert.Equal(new DateOnly(2026, 6, 7), request.StartDate);
        Assert.Equal(new DateOnly(2026, 6, 13), request.EndDate);
    }

    private static Account CreateAccount(decimal balance)
    {
        return new Account(
            Guid.NewGuid(),
            "Checking",
            AccountType.DebitCard,
            balance,
            "EUR",
            DateTimeOffset.UtcNow);
    }

    private static ScheduledTransaction CreateOneTimeScheduledTransaction(
        string name,
        decimal amount,
        TransactionType type,
        DateOnly date,
        Guid accountId)
    {
        return new ScheduledTransaction(
            Guid.NewGuid(),
            name,
            amount,
            accountId,
            null,
            type,
            new RecurrenceRule(
                RecurrenceFrequency.Yearly,
                1,
                date,
                EndDate: date),
            date,
            Active: true);
    }
}
