namespace Banccoon.Core.ImportExport;

public sealed record ExportEnvelope(
    int ExportFormatVersion,
    string ApplicationVersion,
    DateTimeOffset ExportDate,
    ExportData Data);
