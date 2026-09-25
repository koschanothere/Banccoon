using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public sealed class HistoricalBalanceService : IHistoricalBalanceService
{
    public IReadOnlyList<HistoricalBalancePoint> GetHistoricalBalances(
        DateOnly startDate,
        DateOnly endDate,
        decimal currentTotalBalance,
        IReadOnlyCollection<Guid> includedAccountIds,
        IReadOnlyCollection<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(includedAccountIds);
        ArgumentNullException.ThrowIfNull(transactions);

        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be on or after start date.", nameof(endDate));
        }

        var relevantTransactions = transactions
            .Where(transaction => transaction.Date >= startDate && transaction.Date <= endDate)
            .Where(transaction => TouchesIncludedAccount(transaction, includedAccountIds))
            .ToArray();

        // startDate's own transactions are already inside startDate's end-of-day balance, so only
        // later days need reversing - but startDate still reports them as its events below.
        var effectByDate = relevantTransactions
            .Where(transaction => transaction.Date > startDate)
            .GroupBy(transaction => transaction.Date)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(transaction => GetTotalEffect(transaction, includedAccountIds)));

        var eventsByDate = relevantTransactions
            .OrderBy(transaction => transaction.Time ?? TimeOnly.MaxValue)
            .ThenBy(transaction => transaction.Name, StringComparer.OrdinalIgnoreCase)
            .ToLookup(transaction => transaction.Date);

        var balanceByDate = new Dictionary<DateOnly, decimal> { [endDate] = currentTotalBalance };
        for (var date = endDate; date > startDate; date = date.AddDays(-1))
        {
            var effect = effectByDate.GetValueOrDefault(date, 0m);
            balanceByDate[date.AddDays(-1)] = balanceByDate[date] - effect;
        }

        var points = new List<HistoricalBalancePoint>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var events = eventsByDate[date]
                .Select(transaction => new HistoricalBalanceEvent(
                    transaction.Id,
                    transaction.Name,
                    transaction.Type,
                    Math.Abs(transaction.Amount),
                    GetTotalEffect(transaction, includedAccountIds)))
                .ToArray();

            points.Add(new HistoricalBalancePoint(date, balanceByDate[date], events));
        }

        return points;
    }

    private static bool TouchesIncludedAccount(Transaction transaction, IReadOnlyCollection<Guid> includedAccountIds)
    {
        return includedAccountIds.Contains(transaction.AccountId)
            || (transaction.Type == TransactionType.Transfer
                && transaction.DestinationAccountId is { } destinationAccountId
                && includedAccountIds.Contains(destinationAccountId));
    }

    private static decimal GetTotalEffect(Transaction transaction, IReadOnlyCollection<Guid> includedAccountIds)
    {
        if (transaction.Type == TransactionType.Transfer)
        {
            var effect = 0m;
            if (includedAccountIds.Contains(transaction.AccountId))
            {
                effect -= Math.Abs(transaction.Amount);
            }

            if (transaction.DestinationAccountId is { } destinationAccountId
                && includedAccountIds.Contains(destinationAccountId))
            {
                effect += Math.Abs(transaction.Amount);
            }

            return effect;
        }

        if (!includedAccountIds.Contains(transaction.AccountId))
        {
            return 0m;
        }

        return MoneyFlow.GetSignedAmount(transaction.Amount, transaction.Type);
    }
}
