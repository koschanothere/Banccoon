namespace Banccoon.Core.Statements;

// Links between a bank's own operation categories and the app's categories (see BankCategoryLink).
// A linked bank category fills in an imported row's category - unless a learned rule for that
// recipient says otherwise (StatementImportService); links never set the type.
public interface IBankCategoryService
{
    // The statement's bank categories that its parser has never seen before, most used first -
    // what the import's linking step asks about.
    Task<IReadOnlyList<DetectedBankCategory>> GetNewCategoriesAsync(
        ParsedStatement statement,
        CancellationToken cancellationToken = default);

    // Records the linking step's answers: a category id links, null ignores. Categories not in
    // `links` are left as they are.
    Task SaveLinksAsync(
        string parserId,
        IReadOnlyDictionary<string, Guid?> links,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BankCategoryLink>> GetLinksAsync(
        string parserId,
        CancellationToken cancellationToken = default);

    // The parsers (banks) that have seen at least one category, for Settings.
    Task<IReadOnlyList<string>> GetParsersWithCategoriesAsync(CancellationToken cancellationToken = default);
}
