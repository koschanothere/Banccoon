namespace Banccoon.Core.Statements;

public sealed record StatementPreviewResult(
    bool ParserAvailable,
    StatementImportMessage Message,
    ParsedStatement? Statement);
