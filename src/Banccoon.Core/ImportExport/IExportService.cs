namespace Banccoon.Core.ImportExport;

public interface IExportService
{
    Task<ExportEnvelope> CreateExportAsync(CancellationToken cancellationToken = default);
}
