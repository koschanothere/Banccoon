using System.Text.Json;
using System.Text.Json.Serialization;
using Banccoon.Core.ImportExport;

namespace Banccoon.Infrastructure.ImportExport;

public sealed class JsonBackupService : IBackupService
{
    private readonly IExportService exportService;
    private readonly IImportService importService;

    public JsonBackupService(IExportService exportService, IImportService importService)
    {
        this.exportService = exportService;
        this.importService = importService;
    }

    public async Task CreateBackupAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var exportEnvelope = await exportService.CreateExportAsync(cancellationToken);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, exportEnvelope, JsonOptions.Create(), cancellationToken);
    }

    public async Task<ExportEnvelope> ReadBackupAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var exportEnvelope = await JsonSerializer.DeserializeAsync<ExportEnvelope>(
            stream,
            JsonOptions.Create(),
            cancellationToken);

        return exportEnvelope ?? throw new InvalidDataException("Backup file did not contain a valid export envelope.");
    }

    public async Task<ImportResult> RestoreBackupAsync(
        string filePath,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        var exportEnvelope = await ReadBackupAsync(filePath, cancellationToken);
        return await importService.ImportAsync(exportEnvelope, mode, cancellationToken);
    }
}

internal static class JsonOptions
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        return options;
    }
}
