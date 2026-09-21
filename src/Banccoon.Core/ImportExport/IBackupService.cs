namespace Banccoon.Core.ImportExport;

public interface IBackupService
{
    Task CreateBackupAsync(string filePath, CancellationToken cancellationToken = default);

    Task<ExportEnvelope> ReadBackupAsync(string filePath, CancellationToken cancellationToken = default);

    Task<ImportResult> RestoreBackupAsync(
        string filePath,
        ImportMode mode,
        CancellationToken cancellationToken = default);
}
