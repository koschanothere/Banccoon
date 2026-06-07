using Banccoon.Core.Models;

namespace Banccoon.Core.ImportExport;

public sealed record ExportData(
    IReadOnlyList<Account> Accounts,
    IReadOnlyList<Transaction> Transactions,
    IReadOnlyList<ScheduledTransaction> ScheduledTransactions,
    IReadOnlyList<Category> Categories,
    IReadOnlyList<SavingsGoal> SavingsGoals,
    AppSettings Settings);
