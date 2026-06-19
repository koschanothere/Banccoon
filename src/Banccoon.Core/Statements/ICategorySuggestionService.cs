namespace Banccoon.Core.Statements;

public interface ICategorySuggestionService
{
    CategorySuggestion? Suggest(
        ParsedStatementRow row,
        Guid accountId,
        IEnumerable<CategoryLearningRule> rules);

    CategoryLearningRule Learn(
        StatementImportRow row,
        Guid accountId,
        Guid categoryId,
        IEnumerable<CategoryLearningRule> existingRules,
        DateTimeOffset now);

    string Normalize(string value);
}
