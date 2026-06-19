namespace Banccoon.Core.Statements;

public sealed record StatementParseRequest(
    string FilePath,
    Guid AccountId);
