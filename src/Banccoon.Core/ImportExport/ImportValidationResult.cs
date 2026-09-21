namespace Banccoon.Core.ImportExport;

public sealed record ImportValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ImportConflict> Conflicts)
{
    public static ImportValidationResult Success(IReadOnlyList<ImportConflict>? conflicts = null)
    {
        return new ImportValidationResult(true, Array.Empty<string>(), conflicts ?? Array.Empty<ImportConflict>());
    }

    public static ImportValidationResult Failure(IReadOnlyList<string> errors, IReadOnlyList<ImportConflict>? conflicts = null)
    {
        return new ImportValidationResult(false, errors, conflicts ?? Array.Empty<ImportConflict>());
    }
}
