namespace Banccoon.Core.Statements;

public sealed record StatementParserDescriptor(
    string Id,
    string Name,
    IReadOnlyList<string> SupportedFileExtensions);
