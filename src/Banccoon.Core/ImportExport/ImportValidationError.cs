namespace Banccoon.Core.ImportExport;

// Core stays UI-language-agnostic: a code plus the structured facts the App layer needs to compose
// a sentence (which entity, which of its references, the raw ids). Entity/reference kinds are enums
// rather than English nouns so the translated sentence can decline them correctly per language
// instead of slotting a nominative noun into a position that needs a different grammatical case.
public sealed record ImportValidationError(
    ImportValidationErrorCode Code,
    ImportEntityType? EntityType = null,
    Guid? EntityId = null,
    ImportReferenceKind? Reference = null,
    Guid? ReferencedId = null,
    int? FormatVersion = null)
{
    public static ImportValidationError UnsupportedFormatVersion(int formatVersion)
    {
        return new ImportValidationError(ImportValidationErrorCode.UnsupportedFormatVersion, FormatVersion: formatVersion);
    }

    public static ImportValidationError ApplicationVersionRequired()
    {
        return new ImportValidationError(ImportValidationErrorCode.ApplicationVersionRequired);
    }

    public static ImportValidationError DuplicateId(ImportEntityType entityType, Guid entityId)
    {
        return new ImportValidationError(ImportValidationErrorCode.DuplicateId, entityType, entityId);
    }

    public static ImportValidationError MissingReference(
        ImportEntityType entityType,
        Guid entityId,
        ImportReferenceKind reference,
        Guid referencedId)
    {
        return new ImportValidationError(
            ImportValidationErrorCode.MissingReference,
            entityType,
            entityId,
            reference,
            referencedId);
    }
}
