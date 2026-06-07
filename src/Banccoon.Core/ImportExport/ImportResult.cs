namespace Banccoon.Core.ImportExport;

public sealed record ImportResult(
    ImportMode Mode,
    ImportValidationResult Validation,
    int AccountsImported,
    int TransactionsImported,
    int ScheduledTransactionsImported,
    int CategoriesImported,
    int SavingsGoalsImported);
