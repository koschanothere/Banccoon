using System.Globalization;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.Core.Reconciliation;

public sealed class ExpectedTransactionMatcher : IExpectedTransactionMatcher
{
    public IReadOnlyList<Transaction> FindAttachable(
        ForecastEvent expectedEvent,
        IEnumerable<Transaction> transactions,
        string? search,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(expectedEvent);
        ArgumentNullException.ThrowIfNull(transactions);

        var query = search?.Trim() ?? string.Empty;
        return transactions
            .Where(transaction => transaction.PaidScheduledTransactionId is null)
            .Where(transaction => query.Length == 0 || Matches(transaction, query))
            .OrderBy(transaction => DayDistance(transaction.Date, expectedEvent.Date))
            .ThenByDescending(transaction => transaction.Date)
            .ThenBy(transaction => transaction.Id)
            .Take(limit)
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

    // Name or notes containing the text, or - when it reads as a number, however it's written
    // ("1 500", "1500,50") - an amount containing those digits.
    private static bool Matches(Transaction transaction, string query)
    {
        if ((transaction.Name?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || (transaction.Notes?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false))
        {
            return true;
        }

        var digits = string.Concat(query.Where(character => !char.IsWhiteSpace(character))).Replace(',', '.');
        return decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
            && Math.Abs(transaction.Amount).ToString("0.00", CultureInfo.InvariantCulture).Contains(digits, StringComparison.Ordinal);
    }

    private static int DayDistance(DateOnly left, DateOnly right)
    {
        return Math.Abs(left.DayNumber - right.DayNumber);
    }
}
