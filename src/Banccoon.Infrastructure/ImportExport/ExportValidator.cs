using Banccoon.Core.ImportExport;

namespace Banccoon.Infrastructure.ImportExport;

public sealed class ExportValidator : IExportValidator
{
    public ImportValidationResult Validate(ExportEnvelope exportEnvelope)
    {
        ArgumentNullException.ThrowIfNull(exportEnvelope);

        var errors = new List<ImportValidationError>();

        if (exportEnvelope.ExportFormatVersion != ExportFormat.CurrentVersion)
        {
            errors.Add(ImportValidationError.UnsupportedFormatVersion(exportEnvelope.ExportFormatVersion));
        }

        if (string.IsNullOrWhiteSpace(exportEnvelope.ApplicationVersion))
        {
            errors.Add(ImportValidationError.ApplicationVersionRequired());
        }

        var data = exportEnvelope.Data;
        var accountIds = data.Accounts.Select(account => account.Id).ToHashSet();
        var categoryIds = data.Categories.Select(category => category.Id).ToHashSet();
        var transactionIds = data.Transactions.Select(transaction => transaction.Id).ToHashSet();
        var statementBatchIds = data.StatementImportBatches.Select(batch => batch.Id).ToHashSet();

        AddDuplicateErrors(data.Accounts.Select(account => account.Id), ImportEntityType.Account, errors);
        AddDuplicateErrors(data.Categories.Select(category => category.Id), ImportEntityType.Category, errors);
        AddDuplicateErrors(data.Transactions.Select(transaction => transaction.Id), ImportEntityType.Transaction, errors);
        AddDuplicateErrors(data.ScheduledTransactions.Select(transaction => transaction.Id), ImportEntityType.ScheduledTransaction, errors);
        AddDuplicateErrors(data.SavingsGoals.Select(goal => goal.Id), ImportEntityType.SavingsGoal, errors);
        AddDuplicateErrors(data.StatementImportBatches.Select(batch => batch.Id), ImportEntityType.StatementImportBatch, errors);
        AddDuplicateErrors(data.StatementImportRows.Select(row => row.Id), ImportEntityType.StatementImportRow, errors);
        AddDuplicateErrors(data.CategoryLearningRules.Select(rule => rule.Id), ImportEntityType.CategoryLearningRule, errors);

        foreach (var transaction in data.Transactions)
        {
            if (!accountIds.Contains(transaction.AccountId))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.Transaction, transaction.Id, ImportReferenceKind.Account, transaction.AccountId));
            }

            if (transaction.CategoryId.HasValue && !categoryIds.Contains(transaction.CategoryId.Value))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.Transaction, transaction.Id, ImportReferenceKind.Category, transaction.CategoryId.Value));
            }
        }

        foreach (var scheduledTransaction in data.ScheduledTransactions)
        {
            if (!accountIds.Contains(scheduledTransaction.AccountId))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.ScheduledTransaction, scheduledTransaction.Id, ImportReferenceKind.Account, scheduledTransaction.AccountId));
            }

            if (scheduledTransaction.CategoryId.HasValue && !categoryIds.Contains(scheduledTransaction.CategoryId.Value))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.ScheduledTransaction, scheduledTransaction.Id, ImportReferenceKind.Category, scheduledTransaction.CategoryId.Value));
            }
        }

        foreach (var goal in data.SavingsGoals)
        {
            if (goal.AccountId.HasValue && !accountIds.Contains(goal.AccountId.Value))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.SavingsGoal, goal.Id, ImportReferenceKind.Account, goal.AccountId.Value));
            }
        }

        foreach (var batch in data.StatementImportBatches)
        {
            if (!accountIds.Contains(batch.AccountId))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.StatementImportBatch, batch.Id, ImportReferenceKind.Account, batch.AccountId));
            }
        }

        foreach (var row in data.StatementImportRows)
        {
            if (!statementBatchIds.Contains(row.BatchId))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.StatementImportRow, row.Id, ImportReferenceKind.Batch, row.BatchId));
            }

            if (row.SuggestedCategoryId.HasValue && !categoryIds.Contains(row.SuggestedCategoryId.Value))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.StatementImportRow, row.Id, ImportReferenceKind.SuggestedCategory, row.SuggestedCategoryId.Value));
            }

            if (row.CategoryId.HasValue && !categoryIds.Contains(row.CategoryId.Value))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.StatementImportRow, row.Id, ImportReferenceKind.Category, row.CategoryId.Value));
            }

            if (row.DuplicateTransactionId.HasValue && !transactionIds.Contains(row.DuplicateTransactionId.Value))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.StatementImportRow, row.Id, ImportReferenceKind.DuplicateTransaction, row.DuplicateTransactionId.Value));
            }

            if (row.CreatedTransactionId.HasValue && !transactionIds.Contains(row.CreatedTransactionId.Value))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.StatementImportRow, row.Id, ImportReferenceKind.CreatedTransaction, row.CreatedTransactionId.Value));
            }
        }

        foreach (var rule in data.CategoryLearningRules)
        {
            if (!categoryIds.Contains(rule.CategoryId))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.CategoryLearningRule, rule.Id, ImportReferenceKind.Category, rule.CategoryId));
            }

            if (rule.AccountId.HasValue && !accountIds.Contains(rule.AccountId.Value))
            {
                errors.Add(ImportValidationError.MissingReference(ImportEntityType.CategoryLearningRule, rule.Id, ImportReferenceKind.Account, rule.AccountId.Value));
            }
        }

        return errors.Count == 0
            ? ImportValidationResult.Success()
            : ImportValidationResult.Failure(errors);
    }

    private static void AddDuplicateErrors(IEnumerable<Guid> ids, ImportEntityType entityType, List<ImportValidationError> errors)
    {
        var duplicates = ids
            .GroupBy(id => id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicate in duplicates)
        {
            errors.Add(ImportValidationError.DuplicateId(entityType, duplicate));
        }
    }
}
