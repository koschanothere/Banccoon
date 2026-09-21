namespace Banccoon.Core.Statements;

public sealed record StatementPreviewResult(
    bool ParserAvailable,
    string Message,
    ParsedStatement? Statement);
