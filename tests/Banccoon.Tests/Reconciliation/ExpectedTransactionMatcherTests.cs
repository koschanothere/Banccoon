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

    [Fact]
    public void FindCandidates_OffersTheRecordedPaymentForTheOccurrence()
    {
        var payment = CreateTransaction(RentDay.AddDays(1), 700m);

        var candidates = matcher.FindCandidates(CreateRentEvent(), [payment]);

        Assert.Equal(payment, Assert.Single(candidates));
    }

    [Fact]
    public void FindCandidates_ExcludesTransactionsAlreadyLinkedToAnyOccurrence()
    {
        var alreadyLinked = CreateTransaction(RentDay, 700m) with
        {
            PaidScheduledTransactionId = Guid.NewGuid(),
            PaidScheduledOccurrenceDate = RentDay
        };

        Assert.Empty(matcher.FindCandidates(CreateRentEvent(), [alreadyLinked]));
    }

    [Fact]
    public void FindCandidates_ExcludesOtherTypesAndOtherAccounts()
    {
        var income = CreateTransaction(RentDay, 700m, TransactionType.Income);
        var otherAccount = CreateTransaction(RentDay, 700m) with { AccountId = Guid.NewGuid() };

        Assert.Empty(matcher.FindCandidates(CreateRentEvent(), [income, otherAccount]));
    }

    [Fact]
    public void FindCandidates_OnlyWithinAWeekOfTheScheduledDate()
    {
        var justInside = CreateTransaction(RentDay.AddDays(-ExpectedTransactionMatcher.MaxDayDistance), 700m);
        var justOutside = CreateTransaction(RentDay.AddDays(ExpectedTransactionMatcher.MaxDayDistance + 1), 700m);

        var candidates = matcher.FindCandidates(CreateRentEvent(), [justInside, justOutside]);

        Assert.Equal(justInside, Assert.Single(candidates));
    }

    [Fact]
    public void FindCandidates_ToleratesAVaryingBillButNotAnUnrelatedAmount()
    {
        var utilityVariation = CreateTransaction(RentDay, 700m * 1.25m);
        var coffee = CreateTransaction(RentDay, 4.5m);
        var tooHigh = CreateTransaction(RentDay, 700m * 1.26m);

        var candidates = matcher.FindCandidates(CreateRentEvent(), [utilityVariation, coffee, tooHigh]);

        Assert.Equal(utilityVariation, Assert.Single(candidates));
    }

    [Fact]
    public void FindCandidates_RanksClosestAmountFirstThenClosestDate()
    {
        var exactButLater = CreateTransaction(RentDay.AddDays(5), 700m);
        var exactAndSameDay = CreateTransaction(RentDay, 700m);
        var closeAmountSameDay = CreateTransaction(RentDay, 690m);

        var candidates = matcher.FindCandidates(CreateRentEvent(), [exactButLater, closeAmountSameDay, exactAndSameDay]);

        Assert.Equal([exactAndSameDay, exactButLater, closeAmountSameDay], candidates);
    }

    [Fact]
    public void FindCandidates_ReturnsAtMostTheCandidateLimit()
    {
        var many = Enumerable.Range(0, 10).Select(i => CreateTransaction(RentDay, 700m)).ToArray();

        Assert.Equal(ExpectedTransactionMatcher.MaxCandidates, matcher.FindCandidates(CreateRentEvent(), many).Count);
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
