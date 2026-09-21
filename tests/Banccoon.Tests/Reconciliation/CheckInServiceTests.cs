using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Reconciliation;
using Xunit;

namespace Banccoon.Tests.Reconciliation;

public sealed class CheckInServiceTests
{
    private readonly CheckInService service = new(
        new ScheduledTransactionProjectionService(new RecurrenceService()));

    [Fact]
    public void CreateSession_IncludesExpectedScheduledTransactionsInPeriod()
    {
        var accountId = Guid.NewGuid();
        var salary = CreateScheduledTransaction(
            "Salary",
            1500m,
            TransactionType.Income,
            accountId,
            new DateOnly(2026, 6, 10));
        var rent = CreateScheduledTransaction(
            "Rent",
            900m,
            TransactionType.Expense,
            accountId,
            new DateOnly(2026, 6, 12));

        var session = service.CreateSession(
            new DateOnly(2026, 6, 7),
            new DateOnly(2026, 6, 14),
            [salary, rent]);

        Assert.Equal(new DateOnly(2026, 6, 7), session.FromDate);
        Assert.Equal(new DateOnly(2026, 6, 14), session.ToDate);
        Assert.Equal(2, session.ExpectedTransactions.Count);
        Assert.All(session.ExpectedTransactions, review => Assert.Equal(ExpectedTransactionDecision.Pending, review.Decision));
    }

    [Fact]
    public void ExpectedTransactionReview_CanBeConfirmedDelayedOrCancelled()
    {
        var review = new ExpectedTransactionReview(new ForecastEvent(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 10),
            "Rent",
            900m,
            TransactionType.Expense,
            Guid.NewGuid(),
            null,
            ForecastEventKind.ScheduledTransaction));

        Assert.Equal(ExpectedTransactionDecision.Confirmed, review.Confirm().Decision);
        Assert.Equal(ExpectedTransactionDecision.Cancelled, review.Cancel().Decision);

        var delayed = review.DelayUntil(new DateOnly(2026, 6, 15));
        Assert.Equal(ExpectedTransactionDecision.Delayed, delayed.Decision);
        Assert.Equal(new DateOnly(2026, 6, 15), delayed.DelayedUntil);
    }

    [Fact]
    public void CreateSession_WhenDateRangeIsInvalid_Throws()
    {
        Assert.Throws<ArgumentException>(() => service.CreateSession(
            new DateOnly(2026, 6, 14),
            new DateOnly(2026, 6, 7),
            Array.Empty<ScheduledTransaction>()));
    }

    private static ScheduledTransaction CreateScheduledTransaction(
        string name,
        decimal amount,
        TransactionType type,
        Guid accountId,
        DateOnly date)
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
