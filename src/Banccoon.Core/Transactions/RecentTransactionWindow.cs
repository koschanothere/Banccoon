namespace Banccoon.Core.Transactions;

// How much transaction history the app keeps in memory for everyday use (2026-09-25): the current
// calendar month and the eleven before it, plus anything dated later. Older months are read from
// the database only when a page asks for them (ITransactionRepository.GetInRangeAsync) and are not
// kept once that page lets go of them.
public static class RecentTransactionWindow
{
    public const int Months = 12;

    public static DateOnly StartFor(DateOnly today) => new DateOnly(today.Year, today.Month, 1).AddMonths(-(Months - 1));
}
