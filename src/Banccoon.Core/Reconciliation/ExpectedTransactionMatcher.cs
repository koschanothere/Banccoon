using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.Core.Reconciliation;

public sealed class ExpectedTransactionMatcher : IExpectedTransactionMatcher
{
    // How far a real payment's date may drift from its scheduled date and still be offered.
    public const int MaxDayDistance = 7;

    // How far its amount may differ, as a fraction of the scheduled amount - wide enough for
    // bills that vary month to month (utilities), narrow enough that a coffee isn't offered as
    // this month's rent.
    public const decimal AmountTolerance = 0.25m;

    public const int MaxCandidates = 5;

    public IReadOnlyList<Transaction> FindCandidates(ForecastEvent expectedEvent, IEnumerable<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(expectedEvent);
        ArgumentNullException.ThrowIfNull(transactions);

        var expectedAmount = Math.Abs(expectedEvent.Amount);
        var maxAmountDifference = expectedAmount * AmountTolerance;

        return transactions
            .Where(transaction => transaction.PaidScheduledTransactionId is null)
            .Where(transaction => transaction.Type == expectedEvent.Type)
            .Where(transaction => transaction.AccountId == expectedEvent.AccountId)
            .Where(transaction => DayDistance(transaction.Date, expectedEvent.Date) <= MaxDayDistance)
            .Where(transaction => Math.Abs(Math.Abs(transaction.Amount) - expectedAmount) <= maxAmountDifference)
            .OrderBy(transaction => Math.Abs(Math.Abs(transaction.Amount) - expectedAmount))
            .ThenBy(transaction => DayDistance(transaction.Date, expectedEvent.Date))
            .ThenBy(transaction => transaction.Date)
            .ThenBy(transaction => transaction.Id)
            .Take(MaxCandidates)
            .ToArray();
    }

    public Transaction Attach(Transaction transaction, ForecastEvent expectedEvent)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(expectedEvent);

        if (transaction.PaidScheduledTransactionId is not null)
        {
            throw new InvalidOperationException("The transaction is already linked to a scheduled occurrence.");
        }

        return transaction with
        {
            PaidScheduledTransactionId = expectedEvent.SourceId,
            PaidScheduledOccurrenceDate = expectedEvent.Date
        };
    }

    private static int DayDistance(DateOnly left, DateOnly right)
    {
        return Math.Abs(left.DayNumber - right.DayNumber);
    }
}
