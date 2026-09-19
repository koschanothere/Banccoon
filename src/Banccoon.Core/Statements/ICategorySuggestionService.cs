using Banccoon.Core.Models;

namespace Banccoon.Core.Statements;

public interface ICategorySuggestionService
{
    TransactionType? SuggestType(
        ParsedStatementRow row,
        Guid accountId,
        IEnumerable<CategoryLearningRule> rules);

    CategorySuggestion? Suggest(
        ParsedStatementRow row,
        Guid accountId,
        TransactionType type,
        IEnumerable<CategoryLearningRule> rules);

    CategoryLearningRule Learn(
        StatementImportRow row,
        Guid accountId,
        Guid categoryId,
        IEnumerable<CategoryLearningRule> existingRules,
        DateTimeOffset now);

    string Normalize(string value);
}
