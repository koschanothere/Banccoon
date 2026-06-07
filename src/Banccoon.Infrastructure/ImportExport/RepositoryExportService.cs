using Banccoon.Core.ImportExport;
using Banccoon.Core.Repositories;

namespace Banccoon.Infrastructure.ImportExport;

public sealed class RepositoryExportService : IExportService
{
    private readonly IAccountRepository accountRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly IScheduledTransactionRepository scheduledTransactionRepository;
    private readonly ISavingsGoalRepository savingsGoalRepository;
    private readonly ISettingsRepository settingsRepository;

    public RepositoryExportService(
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        IScheduledTransactionRepository scheduledTransactionRepository,
        ISavingsGoalRepository savingsGoalRepository,
        ISettingsRepository settingsRepository)
    {
        this.accountRepository = accountRepository;
        this.categoryRepository = categoryRepository;
        this.transactionRepository = transactionRepository;
        this.scheduledTransactionRepository = scheduledTransactionRepository;
        this.savingsGoalRepository = savingsGoalRepository;
        this.settingsRepository = settingsRepository;
    }

    public async Task<ExportEnvelope> CreateExportAsync(CancellationToken cancellationToken = default)
    {
        var data = new ExportData(
            await accountRepository.GetAllAsync(cancellationToken),
            await transactionRepository.GetAllAsync(cancellationToken),
            await scheduledTransactionRepository.GetAllAsync(cancellationToken),
            await categoryRepository.GetAllAsync(cancellationToken),
            await savingsGoalRepository.GetAllAsync(cancellationToken),
            await settingsRepository.GetAsync(cancellationToken));

        return new ExportEnvelope(
            ExportFormat.CurrentVersion,
            "1.0.0",
            DateTimeOffset.UtcNow,
            data);
    }
}
