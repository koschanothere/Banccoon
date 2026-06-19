using Banccoon.Core.Statements;

namespace Banccoon.Core.Repositories;

public interface IStatementImportRepository
{
    Task<IReadOnlyList<StatementImportBatch>> GetAllBatchesAsync(CancellationToken cancellationToken = default);

    Task<StatementImportBatch?> GetBatchByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatementImportRow>> GetRowsByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<StatementImportRow?> GetRowByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveBatchAsync(StatementImportBatch batch, CancellationToken cancellationToken = default);

    Task SaveRowAsync(StatementImportRow row, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
