using Banccoon.Core.ImportExport;
using Banccoon.Core.Repositories;

namespace Banccoon.Infrastructure.ImportExport;

public sealed class RepositoryImportService : IImportService
{
    private readonly IAccountRepository accountRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly ISavingsGoalRepository savingsGoalRepository;
    private readonly ISettingsRepository settingsRepository;
    private readonly IExportValidator exportValidator;
    private readonly IStatementImportRepository statementImportRepository;
    private readonly ICategoryLearningRuleRepository categoryLearningRuleRepository;
    private readonly IBankCategoryLinkRepository bankCategoryLinkRepository;
    private readonly ILocalDataResetService localDataResetService;

    public RepositoryImportService(
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        ISavingsGoalRepository savingsGoalRepository,
        ISettingsRepository settingsRepository,
        IExportValidator exportValidator,
        IStatementImportRepository statementImportRepository,
        ICategoryLearningRuleRepository categoryLearningRuleRepository,
        IBankCategoryLinkRepository bankCategoryLinkRepository,
        ILocalDataResetService localDataResetService)
    {
        this.accountRepository = accountRepository;
        this.categoryRepository = categoryRepository;
        this.transactionRepository = transactionRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.savingsGoalRepository = savingsGoalRepository;
        this.settingsRepository = settingsRepository;
        this.exportValidator = exportValidator;
        this.statementImportRepository = statementImportRepository;
        this.categoryLearningRuleRepository = categoryLearningRuleRepository;
        this.bankCategoryLinkRepository = bankCategoryLinkRepository;
        this.localDataResetService = localDataResetService;
    }

    public Task<ImportValidationResult> ValidateAsync(
        ExportEnvelope exportEnvelope,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(exportValidator.Validate(exportEnvelope));
    }

    public async Task<ImportResult> ImportAsync(
        ExportEnvelope exportEnvelope,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        var validation = exportValidator.Validate(exportEnvelope);
        if (!validation.IsValid || mode == ImportMode.ValidateOnly)
        {
            return new ImportResult(mode, validation, 0, 0, 0, 0, 0);
        }

        if (mode == ImportMode.Replace)
        {
            await localDataResetService.ResetAllAsync(cancellationToken);
        }

        await SaveDataAsync(exportEnvelope.Data, cancellationToken);

        return new ImportResult(
            mode,
            validation,
            exportEnvelope.Data.Accounts.Count,
            exportEnvelope.Data.Transactions.Count,
            exportEnvelope.Data.ScheduledTransactions.Count,
            exportEnvelope.Data.Categories.Count,
            exportEnvelope.Data.SavingsGoals.Count);
    }

    private async Task SaveDataAsync(ExportData data, CancellationToken cancellationToken)
    {
        foreach (var account in data.Accounts)
        {
            await accountRepository.SaveAsync(account, cancellationToken);
        }

        foreach (var category in data.Categories)
        {
            await categoryRepository.SaveAsync(category, cancellationToken);
        }

        foreach (var scheduledTransaction in data.ScheduledTransactions)
        {
            await scheduledTransactionRepository.SaveAsync(scheduledTransaction, cancellationToken);
        }

        foreach (var transaction in data.Transactions)
        {
            await transactionRepository.SaveAsync(transaction, cancellationToken);
        }

        foreach (var savingsGoal in data.SavingsGoals)
        {
            await savingsGoalRepository.SaveAsync(savingsGoal, cancellationToken);
        }

        foreach (var batch in data.StatementImportBatches)
        {
            await statementImportRepository.SaveBatchAsync(batch, cancellationToken);
        }

        foreach (var row in data.StatementImportRows)
        {
            await statementImportRepository.SaveRowAsync(row, cancellationToken);
        }

        foreach (var rule in data.CategoryLearningRules)
        {
            await categoryLearningRuleRepository.SaveAsync(rule, cancellationToken);
        }

        // A link to a category the backup doesn't have comes back unlinked rather than failing the
        // whole restore - it's a convenience, and the user can link it again in Settings.
        var categoryIds = data.Categories.Select(category => category.Id).ToHashSet();
        await bankCategoryLinkRepository.SaveAllAsync(
            data.BankCategoryLinks
                .Select(link => link.CategoryId is { } id && !categoryIds.Contains(id) ? link with { CategoryId = null } : link)
                .ToList(),
            cancellationToken);

        await settingsRepository.SaveAsync(data.Settings, cancellationToken);
    }
}
