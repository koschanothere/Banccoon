using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public sealed class HistoricalBalanceService : IHistoricalBalanceService
{
    public IReadOnlyList<ProjectedBalancePoint> GetHistoricalBalances(
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

        var effectByDate = transactions
            .Where(transaction => transaction.Date > startDate && transaction.Date <= endDate)
            .GroupBy(transaction => transaction.Date)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(transaction => GetTotalEffect(transaction, includedAccountIds)));

        var balanceByDate = new Dictionary<DateOnly, decimal> { [endDate] = currentTotalBalance };
        for (var date = endDate; date > startDate; date = date.AddDays(-1))
        {
            var effect = effectByDate.GetValueOrDefault(date, 0m);
            balanceByDate[date.AddDays(-1)] = balanceByDate[date] - effect;
        }

        var points = new List<ProjectedBalancePoint>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            points.Add(new ProjectedBalancePoint(date, balanceByDate[date]));
        }

        return points;
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
