namespace Banccoon.Core.ImportExport;

// Which of an entity's foreign keys points at something missing from the backup.
public enum ImportReferenceKind
{
    Account,
    Category,
    Batch,
    SuggestedCategory,
    DuplicateTransaction,
    CreatedTransaction
}
