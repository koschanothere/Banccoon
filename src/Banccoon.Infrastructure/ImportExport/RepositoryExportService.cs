using Banccoon.Core.ImportExport;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;

namespace Banccoon.Infrastructure.ImportExport;

public sealed class RepositoryExportService : IExportService
{
    private readonly IAccountRepository accountRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly ISavingsGoalRepository savingsGoalRepository;
    private readonly ISettingsRepository settingsRepository;
    private readonly IStatementImportRepository statementImportRepository;
    private readonly ICategoryLearningRuleRepository categoryLearningRuleRepository;

    public RepositoryExportService(
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        ISavingsGoalRepository savingsGoalRepository,
        ISettingsRepository settingsRepository,
        IStatementImportRepository statementImportRepository,
        ICategoryLearningRuleRepository categoryLearningRuleRepository)
    {
        this.accountRepository = accountRepository;
        this.categoryRepository = categoryRepository;
        this.transactionRepository = transactionRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.savingsGoalRepository = savingsGoalRepository;
        this.settingsRepository = settingsRepository;
        this.statementImportRepository = statementImportRepository;
        this.categoryLearningRuleRepository = categoryLearningRuleRepository;
    }

    public async Task<ExportEnvelope> CreateExportAsync(CancellationToken cancellationToken = default)
    {
        var statementImportBatches = await statementImportRepository.GetAllBatchesAsync(cancellationToken);
        var statementImportRows = new List<StatementImportRow>();
        foreach (var batch in statementImportBatches)
        {
            statementImportRows.AddRange(await statementImportRepository.GetRowsByBatchIdAsync(batch.Id, cancellationToken));
        }

        var data = new ExportData(
            await accountRepository.GetAllAsync(cancellationToken),
            await transactionRepository.GetAllAsync(cancellationToken),
            await scheduledTransactionRepository.GetAllAsync(cancellationToken),
            await categoryRepository.GetAllAsync(cancellationToken),
            await savingsGoalRepository.GetAllAsync(cancellationToken),
            await settingsRepository.GetAsync(cancellationToken))
        {
            StatementImportBatches = statementImportBatches,
            StatementImportRows = statementImportRows,
            CategoryLearningRules = await categoryLearningRuleRepository.GetAllAsync(cancellationToken)
        };

        return new ExportEnvelope(
            ExportFormat.CurrentVersion,
            "1.0.0",
            DateTimeOffset.UtcNow,
            data);
    }
}
