using Banccoon.App.Localization;
using Banccoon.App.ViewModels;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;
using Banccoon.Tests.Infrastructure;
using Xunit;

// 2026-09-25: Settings shows learned rules 25 at a time.
public sealed class LearnedRulesPagingTests
{
    [Fact]
    public async Task SixtyRules_ShowTwentyFivePerPage_AndSearchStartsAtPageOne()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var category = new Category(Guid.NewGuid(), "Food", TransactionType.Expense);
        await store.Categories.SaveAsync(category);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= 60; i++)
        {
            // Newest first on screen: rule 60 is the most recently updated.
            await store.CategoryLearningRules.SaveAsync(new CategoryLearningRule(
                Guid.NewGuid(), $"Shop {i:00}", $"SHOP {i:00}", TransactionType.Expense, category.Id, null, null, 1, start, start.AddMinutes(i)));
        }

        var rules = new CategoryLearningRulesViewModel(store.CategoryLearningRules, store.Categories, store.Accounts);
        await rules.InitializeAsync();

        Assert.Equal(25, rules.Rows.Count);
        Assert.Equal("Shop 60", rules.Rows[0].MatchText);
        Assert.Equal(string.Format(Translator.Get("Settings_LearnedRulesPageFormat"), 1, 25, 60), rules.PageText);
        Assert.True(rules.HasPages);
        Assert.False(rules.CanGoToPreviousPage);

        rules.NextPageCommand.Execute(null);
        rules.NextPageCommand.Execute(null);
        Assert.Equal(10, rules.Rows.Count);
        Assert.Equal("Shop 10", rules.Rows[0].MatchText);
        Assert.Equal(string.Format(Translator.Get("Settings_LearnedRulesPageFormat"), 51, 60, 60), rules.PageText);
        Assert.False(rules.CanGoToNextPage);

        rules.SearchText = "Shop 5";
        Assert.Equal(["Shop 59", "Shop 58", "Shop 57", "Shop 56", "Shop 55", "Shop 54", "Shop 53", "Shop 52", "Shop 51", "Shop 50"], rules.Rows.Select(r => r.MatchText));
        Assert.False(rules.HasPages);
    }
}
