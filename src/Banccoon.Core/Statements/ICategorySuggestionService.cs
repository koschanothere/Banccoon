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

    // Only meaningful once a row has resolved to Transfer - looks for a rule learned for this
    // same recipient that also remembers which of the user's own accounts it was a transfer to.
    Guid? SuggestDestinationAccount(
        ParsedStatementRow row,
        Guid accountId,
        IEnumerable<CategoryLearningRule> rules);

    CategoryLearningRule Learn(
        StatementImportRow row,
        Guid accountId,
        Guid categoryId,
        Guid? destinationAccountId,
        IEnumerable<CategoryLearningRule> existingRules,
        DateTimeOffset now);

    string Normalize(string value);
}
