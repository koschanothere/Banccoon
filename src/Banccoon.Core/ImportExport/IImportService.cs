namespace Banccoon.Core.ImportExport;

public interface IImportService
{
    Task<ImportValidationResult> ValidateAsync(ExportEnvelope exportEnvelope, CancellationToken cancellationToken = default);

    Task<ImportResult> ImportAsync(
        ExportEnvelope exportEnvelope,
        ImportMode mode,
        CancellationToken cancellationToken = default);
}
