namespace Banccoon.Core.Statements;

// Core stays UI-language-agnostic: what happened (Code) plus the one number some messages carry
// (Count, for RowsFound/RowsReadyForReview) - the App layer composes and pluralizes the actual
// sentence via its Translator, same pattern as RecurrenceValidationErrorCode.
public sealed record StatementImportMessage(StatementImportMessageCode Code, int? Count = null);
