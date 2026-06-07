using Banccoon.Core.ImportExport;

namespace Banccoon.Infrastructure.ImportExport;

public sealed class ExportValidator : IExportValidator
{
    public ImportValidationResult Validate(ExportEnvelope exportEnvelope)
    {
        ArgumentNullException.ThrowIfNull(exportEnvelope);

        var errors = new List<string>();

        if (exportEnvelope.ExportFormatVersion != ExportFormat.CurrentVersion)
        {
            errors.Add($"Unsupported export format version: {exportEnvelope.ExportFormatVersion}.");
        }

        if (string.IsNullOrWhiteSpace(exportEnvelope.ApplicationVersion))
        {
            errors.Add("Application version is required.");
        }

        var data = exportEnvelope.Data;
        var accountIds = data.Accounts.Select(account => account.Id).ToHashSet();
        var categoryIds = data.Categories.Select(category => category.Id).ToHashSet();

        AddDuplicateErrors(data.Accounts.Select(account => account.Id), "Account", errors);
        AddDuplicateErrors(data.Categories.Select(category => category.Id), "Category", errors);
        AddDuplicateErrors(data.Transactions.Select(transaction => transaction.Id), "Transaction", errors);
        AddDuplicateErrors(data.ScheduledTransactions.Select(transaction => transaction.Id), "Scheduled transaction", errors);
        AddDuplicateErrors(data.SavingsGoals.Select(goal => goal.Id), "Savings goal", errors);

        foreach (var transaction in data.Transactions)
        {
            if (!accountIds.Contains(transaction.AccountId))
            {
                errors.Add($"Transaction {transaction.Id} references missing account {transaction.AccountId}.");
            }

            if (transaction.CategoryId.HasValue && !categoryIds.Contains(transaction.CategoryId.Value))
            {
                errors.Add($"Transaction {transaction.Id} references missing category {transaction.CategoryId.Value}.");
            }
        }

        foreach (var scheduledTransaction in data.ScheduledTransactions)
        {
            if (!accountIds.Contains(scheduledTransaction.AccountId))
            {
                errors.Add($"Scheduled transaction {scheduledTransaction.Id} references missing account {scheduledTransaction.AccountId}.");
            }

            if (scheduledTransaction.CategoryId.HasValue && !categoryIds.Contains(scheduledTransaction.CategoryId.Value))
            {
                errors.Add($"Scheduled transaction {scheduledTransaction.Id} references missing category {scheduledTransaction.CategoryId.Value}.");
            }
        }

        foreach (var goal in data.SavingsGoals)
        {
            if (goal.AccountId.HasValue && !accountIds.Contains(goal.AccountId.Value))
            {
                errors.Add($"Savings goal {goal.Id} references missing account {goal.AccountId.Value}.");
            }
        }

        return errors.Count == 0
            ? ImportValidationResult.Success()
            : ImportValidationResult.Failure(errors);
    }

    private static void AddDuplicateErrors(IEnumerable<Guid> ids, string entityName, List<string> errors)
    {
        var duplicates = ids
            .GroupBy(id => id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicate in duplicates)
        {
            errors.Add($"{entityName} id {duplicate} appears more than once.");
        }
    }
}
