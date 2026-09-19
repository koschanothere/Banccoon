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

        var suggestion = service.Suggest(row, accountId, TransactionType.Expense, [rule]);

        Assert.NotNull(suggestion);
        Assert.Equal(categoryId, suggestion.CategoryId);
    }

    [Fact]
    public void Suggest_TrimsTrailingStoreNumberAndLocation_SoDifferentBranchesMatch()
    {
        var accountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var rule = new CategoryLearningRule(
            Guid.NewGuid(),
            "PYATEROCHKA",
            service.Normalize("PYATEROCHKA"),
            TransactionType.Expense,
            categoryId,
            accountId,
            AmountHint: null,
            MatchCount: 1,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow);
        var otherBranch = new ParsedStatementRow(
            new DateOnly(2026, 6, 10),
            500m,
            TransactionType.Expense,
            "Card purchase",
            "PYATEROCHKA 30142 SPB RUS");

        var suggestion = service.Suggest(otherBranch, accountId, TransactionType.Expense, [rule]);

        Assert.NotNull(suggestion);
        Assert.Equal(categoryId, suggestion.CategoryId);
    }

    [Fact]
    public void SuggestType_ReturnsLearnedTypeEvenWhenRowGuessedDifferently()
    {
        // A "Перевод" style row always parses as Expense/Income from the +/- sign alone, but a
        // past correction for this recipient (e.g. it's really a Transfer to the user's own
        // savings account) should override that raw guess.
        var accountId = Guid.NewGuid();
        var rule = new CategoryLearningRule(
            Guid.NewGuid(),
            "Perevod Sberezheniya",
            service.Normalize("Perevod Sberezheniya"),
            TransactionType.Transfer,
            Guid.NewGuid(),
            accountId,
            AmountHint: null,
            MatchCount: 4,
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(-1));
        var row = new ParsedStatementRow(
            new DateOnly(2026, 6, 12),
            1000m,
            TransactionType.Expense,
            "Perevod Sberezheniya");

        var suggestedType = service.SuggestType(row, accountId, [rule]);

        Assert.Equal(TransactionType.Transfer, suggestedType);
    }

    [Fact]
    public void SuggestType_ReturnsNullWhenNothingLearnedYet()
    {
        var accountId = Guid.NewGuid();
        var row = new ParsedStatementRow(
            new DateOnly(2026, 6, 12),
            1000m,
            TransactionType.Expense,
            "Unknown Recipient");

        var suggestedType = service.SuggestType(row, accountId, []);

        Assert.Null(suggestedType);
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
