using System.Globalization;
using Banccoon.Core.Models;

namespace Banccoon.Core.Statements;

public sealed class CategorySuggestionService : ICategorySuggestionService
{
    // Rows straight off a parser only ever say Expense or Income (from the +/- sign) - the parser
    // has no way to know a given recipient is usually a Transfer between the user's own accounts.
    // This scans learning rules for the same recipient across ALL types (unlike Suggest, which is
    // scoped to one type) so a past correction ("this recipient is actually a Transfer/Income")
    // can override the raw sign-based guess before category suggestion ever runs.
    public TransactionType? SuggestType(
        ParsedStatementRow row,
        Guid accountId,
        IEnumerable<CategoryLearningRule> rules)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(rules);

        var normalizedText = Normalize(GetMatchText(row.Description, row.Counterparty));
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return null;
        }

        return rules
            .Select(rule => new
            {
                Rule = rule,
                Score = GetScore(rule, accountId, normalizedText, Math.Abs(row.Amount))
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Rule.MatchCount)
            .ThenByDescending(match => match.Rule.UpdatedAt)
            .Select(match => (TransactionType?)match.Rule.Type)
            .FirstOrDefault();
    }

    public CategorySuggestion? Suggest(
        ParsedStatementRow row,
        Guid accountId,
        TransactionType type,
        IEnumerable<CategoryLearningRule> rules)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(rules);

        var normalizedText = Normalize(GetMatchText(row.Description, row.Counterparty));
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return null;
        }

        return rules
            .Where(rule => rule.Type == type)
            .Select(rule => new
            {
                Rule = rule,
                Score = GetScore(rule, accountId, normalizedText, Math.Abs(row.Amount))
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Rule.MatchCount)
            .ThenByDescending(match => match.Rule.UpdatedAt)
            .Select(match => new CategorySuggestion(match.Rule.CategoryId, match.Rule.Id, match.Score))
            .FirstOrDefault();
    }

    public CategoryLearningRule Learn(
        StatementImportRow row,
        Guid accountId,
        Guid categoryId,
        IEnumerable<CategoryLearningRule> existingRules,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(existingRules);

        var matchText = GetMatchText(row.Description, row.Counterparty);
        var normalizedText = Normalize(matchText);
        var existingRule = existingRules.FirstOrDefault(rule =>
            rule.Type == row.Type
            && rule.AccountId == accountId
            && string.Equals(rule.NormalizedMatchText, normalizedText, StringComparison.OrdinalIgnoreCase));

        return existingRule is null
            ? new CategoryLearningRule(
                Guid.NewGuid(),
                matchText,
                normalizedText,
                row.Type,
                categoryId,
                accountId,
                Math.Abs(row.Amount),
                MatchCount: 1,
                now,
                now)
            : existingRule with
            {
                CategoryId = categoryId,
                AmountHint = Math.Abs(row.Amount),
                MatchCount = existingRule.MatchCount + 1,
                UpdatedAt = now
            };
    }

    public string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .Trim()
            .ToUpperInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();

        return string.Join(
            ' ',
            new string(chars)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string GetMatchText(string description, string? counterparty)
    {
        var text = string.IsNullOrWhiteSpace(counterparty)
            ? description.Trim()
            : counterparty.Trim();
        return TrimToCoreRecipientName(text);
    }

    // Statement descriptions often trail off into a store/reference number and sometimes a
    // city/country code (e.g. "PYATEROCHKA 25673 MOSCOW RUS"). Truncating at the first
    // purely-numeric token keeps different branches/locations of the same recipient recognizable
    // as one recipient for learning, without needing a directory of city/country names.
    private static string TrimToCoreRecipientName(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var coreWords = words.TakeWhile(word => !IsNumericToken(word)).ToArray();
        return coreWords.Length == 0 ? text : string.Join(' ', coreWords);
    }

    private static bool IsNumericToken(string word)
    {
        var trimmed = word.Trim('.', ',', '#');
        return trimmed.Length > 0 && trimmed.All(character => char.IsDigit(character) || character is '.' or ',');
    }

    private static int GetScore(
        CategoryLearningRule rule,
        Guid accountId,
        string normalizedText,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(rule.NormalizedMatchText))
        {
            return 0;
        }

        var score = 0;
        if (string.Equals(rule.NormalizedMatchText, normalizedText, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }
        else if (normalizedText.Contains(rule.NormalizedMatchText, StringComparison.OrdinalIgnoreCase)
            || rule.NormalizedMatchText.Contains(normalizedText, StringComparison.OrdinalIgnoreCase))
        {
            score += 70;
        }

        if (score == 0)
        {
            return 0;
        }

        if (rule.AccountId == accountId)
        {
            score += 20;
        }
        else if (rule.AccountId.HasValue)
        {
            score -= 10;
        }

        if (rule.AmountHint.HasValue && decimal.Round(rule.AmountHint.Value, 2) == decimal.Round(amount, 2))
        {
            score += 10;
        }

        return score + Math.Min(rule.MatchCount, 10);
    }
}
