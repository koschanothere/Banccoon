using Banccoon.Core.Repositories;

namespace Banccoon.Core.Statements;

public sealed class BankCategoryService : IBankCategoryService
{
    private readonly IBankCategoryLinkRepository bankCategoryLinkRepository;

    public BankCategoryService(IBankCategoryLinkRepository bankCategoryLinkRepository)
    {
        this.bankCategoryLinkRepository = bankCategoryLinkRepository;
    }

    public async Task<IReadOnlyList<DetectedBankCategory>> GetNewCategoriesAsync(
        ParsedStatement statement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var known = (await bankCategoryLinkRepository.GetByParserAsync(statement.ParserId, cancellationToken))
            .Select(link => link.BankCategory)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return statement.Rows
            .Select(row => row.BankCategory?.Trim())
            .Where(name => !string.IsNullOrEmpty(name) && !known.Contains(name))
            .GroupBy(name => name!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DetectedBankCategory(group.First()!, group.Count()))
            .OrderByDescending(category => category.OperationCount)
            .ThenBy(category => category.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    public async Task SaveLinksAsync(
        string parserId,
        IReadOnlyDictionary<string, Guid?> links,
        CancellationToken cancellationToken = default)
    {
        var existing = (await bankCategoryLinkRepository.GetByParserAsync(parserId, cancellationToken))
            .ToDictionary(link => link.BankCategory, StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var saved = links
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .Select(entry => existing.TryGetValue(entry.Key.Trim(), out var link)
                ? link with { CategoryId = entry.Value }
                : new BankCategoryLink(parserId, entry.Key.Trim(), entry.Value, now))
            .ToList();

        await bankCategoryLinkRepository.SaveAllAsync(saved, cancellationToken);
    }

    public Task<IReadOnlyList<BankCategoryLink>> GetLinksAsync(
        string parserId,
        CancellationToken cancellationToken = default)
    {
        return bankCategoryLinkRepository.GetByParserAsync(parserId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetParsersWithCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return (await bankCategoryLinkRepository.GetAllAsync(cancellationToken))
            .Select(link => link.ParserId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
