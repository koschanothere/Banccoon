namespace Banccoon.Core.ImportExport;

public interface IExportValidator
{
    ImportValidationResult Validate(ExportEnvelope exportEnvelope);
}
