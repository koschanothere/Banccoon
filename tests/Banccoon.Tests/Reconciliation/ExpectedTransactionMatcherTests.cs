using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Reconciliation;
using Xunit;

namespace Banccoon.Tests.Reconciliation;

public sealed class ExpectedTransactionMatcherTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid ScheduleId = Guid.NewGuid();
    private static readonly DateOnly RentDay = new(2026, 6, 10);

    private readonly ExpectedTransactionMatcher matcher = new();

    // 2026-09-25: no more guessing - anything recorded and not yet linked can be attached, nearest
    // the due date first, and the user narrows it by typing.
    [Fact]
    public void FindAttachable_OffersEveryUnlinkedTransaction_WhateverItsAccountTypeOrAmount_NearestTheDueDateFirst()
    {
        var coffee = CreateTransaction(RentDay.AddDays(1), 4m) with { Name = "Coffee" };
        var salary = CreateTransaction(RentDay.AddDays(-2), 90000m, TransactionType.Income) with { Name = "Salary" };
        var otherAccount = CreateTransaction(RentDay.AddDays(40), 700m) with { AccountId = Guid.NewGuid(), Name = "Rent from savings" };
        var sameDay = CreateTransaction(RentDay, 12m) with { Name = "Bus" };
        var linked = CreateTransaction(RentDay, 700m) with { PaidScheduledTransactionId = Guid.NewGuid(), PaidScheduledOccurrenceDate = RentDay };

        var attachable = matcher.FindAttachable(CreateRentEvent(), [coffee, salary, otherAccount, sameDay, linked], search: null, limit: 10);

        Assert.Equal([sameDay, coffee, salary, otherAccount], attachable);
    }

    [Theory]
    [InlineData("rent", "Rent from savings")]
    [InlineData("RENT", "Rent from savings")]
    [InlineData("1 500", "Utilities")]
    [InlineData("1500,5", "Utilities")]
    [InlineData("landlord", "Transfer")]
    public void FindAttachable_NarrowsByNameNotesOrAmount(string search, string expected)
    {
        var rent = CreateTransaction(RentDay.AddDays(3), 700m) with { Name = "Rent from savings" };
        var utilities = CreateTransaction(RentDay, 1500.50m) with { Name = "Utilities" };
        var transfer = CreateTransaction(RentDay, 50m) with { Name = "Transfer", Notes = "Paid the landlord's deposit" };

        var attachable = matcher.FindAttachable(CreateRentEvent(), [rent, utilities, transfer], search, limit: 10);

        Assert.Equal(expected, Assert.Single(attachable).Name);
    }

    [Fact]
    public void FindAttachable_ReturnsAtMostTheLimit()
    {
        var many = Enumerable.Range(0, 20).Select(day => CreateTransaction(RentDay.AddDays(day), 10m)).ToArray();

        Assert.Equal(8, matcher.FindAttachable(CreateRentEvent(), many, search: "", limit: 8).Count);
    }

    [Fact]
    public void Attach_LinksTheTransactionToTheOccurrenceWithoutChangingAnythingElse()
    {
        var payment = CreateTransaction(RentDay.AddDays(1), 700m);

        var attached = matcher.Attach(payment, CreateRentEvent());

        Assert.Equal(ScheduleId, attached.PaidScheduledTransactionId);
        Assert.Equal(RentDay, attached.PaidScheduledOccurrenceDate);
        Assert.Equal(payment with { PaidScheduledTransactionId = null, PaidScheduledOccurrenceDate = null }, attached with { PaidScheduledTransactionId = null, PaidScheduledOccurrenceDate = null });
    }

    [Fact]
    public void Attach_RefusesATransactionAlreadyLinkedElsewhere()
    {
        var linked = CreateTransaction(RentDay, 700m) with { PaidScheduledTransactionId = Guid.NewGuid(), PaidScheduledOccurrenceDate = RentDay };

        Assert.Throws<InvalidOperationException>(() => matcher.Attach(linked, CreateRentEvent()));
    }

    [Fact]
    public void AttachedOccurrence_IsTreatedAsPaidByTheForecast()
    {
        var schedule = new ScheduledTransaction(
            ScheduleId,
            "Rent",
            700m,
            AccountId,
            null,
            TransactionType.Expense,
            new Banccoon.Core.Recurrence.RecurrenceRule(Banccoon.Core.Recurrence.RecurrenceFrequency.Monthly, 1, RentDay, DayOfMonth: 10),
            RentDay,
            Active: true);
        var account = new Account(AccountId, "Card", AccountType.DebitCard, 1000m, "RUB", DateTimeOffset.UtcNow);
        var attached = matcher.Attach(CreateTransaction(RentDay, 700m), CreateRentEvent());
        var forecastService = new ForecastService(
            new AccountBalanceService(),
            new ScheduledTransactionProjectionService(new Banccoon.Core.Recurrence.RecurrenceService()));

        var unattached = forecastService.CreateForecast(new ForecastRequest(RentDay, RentDay, [account], [schedule], Transactions: [CreateTransaction(RentDay, 700m)]));
        var forecast = forecastService.CreateForecast(new ForecastRequest(RentDay, RentDay, [account], [schedule], Transactions: [attached]));

        Assert.Contains(unattached.Events, forecastEvent => forecastEvent.SourceId == ScheduleId && forecastEvent.Date == RentDay);
        Assert.DoesNotContain(forecast.Events, forecastEvent => forecastEvent.SourceId == ScheduleId && forecastEvent.Date == RentDay);
    }

    private static ForecastEvent CreateRentEvent()
    {
        return new ForecastEvent(ScheduleId, RentDay, "Rent", 700m, TransactionType.Expense, AccountId, null, ForecastEventKind.ScheduledTransaction);
    }

    private static Transaction CreateTransaction(DateOnly date, decimal amount, TransactionType type = TransactionType.Expense)
    {
        return new Transaction(Guid.NewGuid(), date, amount, AccountId, null, null, type, Name: "Imported row");
    }
}
