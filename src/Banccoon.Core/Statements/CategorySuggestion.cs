namespace Banccoon.Core.Statements;

public sealed record CategorySuggestion(
    Guid CategoryId,
    Guid RuleId,
    int Score);
