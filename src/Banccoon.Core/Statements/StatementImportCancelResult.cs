namespace Banccoon.Core.Statements;

public sealed record StatementImportCancelResult(
    bool Cancelled,
    StatementImportMessage Message);
