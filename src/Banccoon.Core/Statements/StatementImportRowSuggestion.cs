using Banccoon.Core.Models;

namespace Banccoon.Core.Statements;

// What Banccoon would suggest for one pending statement row from the learning rules as they stand
// right now (see IStatementImportService.GetPendingSuggestionsAsync). DestinationAccountId is the
// other account of a Transfer, same meaning as StatementImportRow.DestinationAccountId.
public sealed record StatementImportRowSuggestion(
    Guid RowId,
    TransactionType Type,
    Guid? CategoryId,
    Guid? DestinationAccountId);
