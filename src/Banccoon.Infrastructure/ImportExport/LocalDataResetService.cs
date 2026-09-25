using Banccoon.Core.ImportExport;
using Banccoon.Core.Repositories;

namespace Banccoon.Infrastructure.ImportExport;

public sealed class LocalDataResetService : ILocalDataResetService
{
    private readonly IAccountRepository accountRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly ISavingsGoalRepository savingsGoalRepository;
    private readonly IStatementImportRepository statementImportRepository;
    private readonly ICategoryLearningRuleRepository categoryLearningRuleRepository;
    private readonly IBankCategoryLinkRepository bankCategoryLinkRepository;

    public LocalDataResetService(
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        ISavingsGoalRepository savingsGoalRepository,
        IStatementImportRepository statementImportRepository,
        ICategoryLearningRuleRepository categoryLearningRuleRepository,
        IBankCategoryLinkRepository bankCategoryLinkRepository)
    {
        this.accountRepository = accountRepository;
        this.categoryRepository = categoryRepository;
        this.transactionRepository = transactionRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.savingsGoalRepository = savingsGoalRepository;
        this.statementImportRepository = statementImportRepository;
        this.categoryLearningRuleRepository = categoryLearningRuleRepository;
        this.bankCategoryLinkRepository = bankCategoryLinkRepository;
    }

    // Deletion order matters here (foreign-key dependents first); Settings is deliberately left
    // untouched - user preferences (theme, etc.) aren't "local financial data" in the sense this
    // is meant to clear.
    public async Task ResetAllAsync(CancellationToken cancellationToken = default)
    {
        await statementImportRepository.DeleteAllAsync(cancellationToken);
        await categoryLearningRuleRepository.DeleteAllAsync(cancellationToken);
        await bankCategoryLinkRepository.DeleteAllAsync(cancellationToken);
        await transactionRepository.DeleteAllAsync(cancellationToken);
        await scheduledTransactionRepository.DeleteAllAsync(cancellationToken);
        await savingsGoalRepository.DeleteAllAsync(cancellationToken);
        await categoryRepository.DeleteAllAsync(cancellationToken);
        await accountRepository.DeleteAllAsync(cancellationToken);
    }
}
