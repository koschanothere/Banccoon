using Banccoon.Core.Models;

namespace Banccoon.Core.Forecasting;

public sealed record ForecastRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyCollection<Account> Accounts,
    IReadOnlyCollection<ScheduledTransaction> ScheduledTransactions,
    IReadOnlyCollection<SavingsGoal>? SavingsGoals = null,
    IReadOnlyCollection<Transaction>? Transactions = null)
{
    public static ForecastRequest ForPeriod(
        DateOnly startDate,
        ForecastPeriod period,
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<ScheduledTransaction> scheduledTransactions)
    {
        return new ForecastRequest(
            startDate,
            startDate.AddDays((int)period - 1),
            accounts,
            scheduledTransactions);
    }
}
