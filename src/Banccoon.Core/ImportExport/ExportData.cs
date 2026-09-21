using Banccoon.Core.Models;
using Banccoon.Core.Statements;

namespace Banccoon.Core.ImportExport;

public sealed record ExportData(
    IReadOnlyList<Account> Accounts,
    IReadOnlyList<Transaction> Transactions,
    IReadOnlyList<ScheduledTransaction> ScheduledTransactions,
    IReadOnlyList<Category> Categories,
    IReadOnlyList<SavingsGoal> SavingsGoals,
    AppSettings Settings)
{
    public IReadOnlyList<StatementImportBatch> StatementImportBatches { get; init; } =
        Array.Empty<StatementImportBatch>();

    public IReadOnlyList<StatementImportRow> StatementImportRows { get; init; } =
        Array.Empty<StatementImportRow>();

    public IReadOnlyList<CategoryLearningRule> CategoryLearningRules { get; init; } =
        Array.Empty<CategoryLearningRule>();
}
