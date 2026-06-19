using Banccoon.Core.Models;

namespace Banccoon.Core.Statements;

public sealed record CategoryLearningRule(
    Guid Id,
    string MatchText,
    string NormalizedMatchText,
    TransactionType Type,
    Guid CategoryId,
    Guid? AccountId,
    decimal? AmountHint,
    int MatchCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
