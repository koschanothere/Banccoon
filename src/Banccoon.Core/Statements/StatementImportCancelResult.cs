namespace Banccoon.Core.Statements;

public sealed record StatementImportCancelResult(
    bool Cancelled,
    string Message);
