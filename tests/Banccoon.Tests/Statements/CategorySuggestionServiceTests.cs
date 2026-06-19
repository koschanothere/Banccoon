using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Xunit;

namespace Banccoon.Tests.Statements;

public sealed class CategorySuggestionServiceTests
{
    private readonly CategorySuggestionService service = new();

    [Fact]
    public void Suggest_MatchesNormalizedCounterparty()
    {
        var accountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var rule = new CategoryLearningRule(
            Guid.NewGuid(),
            "Coffee Shop",
            service.Normalize("Coffee Shop"),
            TransactionType.Expense,
            categoryId,
            accountId,
            4.50m,
            MatchCount: 3,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow);
        var row = new ParsedStatementRow(
            new DateOnly(2026, 6, 10),
            4.50m,
            TransactionType.Expense,
            "Card purchase",
            "coffee-shop #123");

        var suggestion = service.Suggest(row, accountId, [rule]);

        Assert.NotNull(suggestion);
        Assert.Equal(categoryId, suggestion.CategoryId);
    }

    [Fact]
    public void Learn_UpdatesExistingRuleForSameAccountAndMerchant()
    {
        var accountId = Guid.NewGuid();
        var oldCategoryId = Guid.NewGuid();
        var newCategoryId = Guid.NewGuid();
        var existing = new CategoryLearningRule(
            Guid.NewGuid(),
            "Grocer",
            service.Normalize("Grocer"),
            TransactionType.Expense,
            oldCategoryId,
            accountId,
            20m,
            MatchCount: 2,
            DateTimeOffset.UtcNow.AddDays(-4),
            DateTimeOffset.UtcNow.AddDays(-1));
        var row = new StatementImportRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 11),
            22m,
            TransactionType.Expense,
            "Grocer",
            service.Normalize("Grocer"),
            null,
            null,
            null,
            null,
            null,
            StatementImportRowStatus.Pending,
            IsDuplicate: false,
            null,
            null);

        var learned = service.Learn(row, accountId, newCategoryId, [existing], DateTimeOffset.UtcNow);

        Assert.Equal(existing.Id, learned.Id);
        Assert.Equal(newCategoryId, learned.CategoryId);
        Assert.Equal(3, learned.MatchCount);
    }
}
