using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;
using Banccoon.Infrastructure.Database;

namespace Banccoon.Infrastructure.Repositories;

public sealed class SqliteCategoryLearningRuleRepository : SqliteRepositoryBase, ICategoryLearningRuleRepository
{
    public SqliteCategoryLearningRuleRepository(
        ISqliteConnectionFactory connectionFactory,
        IBanccoonDatabaseInitializer databaseInitializer)
        : base(connectionFactory, databaseInitializer)
    {
    }

    public async Task<IReadOnlyList<CategoryLearningRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, MatchText, NormalizedMatchText, Type, CategoryId, AccountId, AmountHint, MatchCount, CreatedAt, UpdatedAt
            FROM CategoryLearningRules
            ORDER BY UpdatedAt DESC;
            """;

        var rules = new List<CategoryLearningRule>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(ReadRule(reader));
        }

        return rules;
    }

    public async Task<CategoryLearningRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, MatchText, NormalizedMatchText, Type, CategoryId, AccountId, AmountHint, MatchCount, CreatedAt, UpdatedAt
            FROM CategoryLearningRules
            WHERE Id = @Id;
            """;
        AddParameter(command, "@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRule(reader) : null;
    }

    public async Task SaveAsync(CategoryLearningRule rule, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CategoryLearningRules (
                Id,
                MatchText,
                NormalizedMatchText,
                Type,
                CategoryId,
                AccountId,
                AmountHint,
                MatchCount,
                CreatedAt,
                UpdatedAt
            )
            VALUES (
                @Id,
                @MatchText,
                @NormalizedMatchText,
                @Type,
                @CategoryId,
                @AccountId,
                @AmountHint,
                @MatchCount,
                @CreatedAt,
                @UpdatedAt
            )
            ON CONFLICT(Id) DO UPDATE SET
                MatchText = excluded.MatchText,
                NormalizedMatchText = excluded.NormalizedMatchText,
                Type = excluded.Type,
                CategoryId = excluded.CategoryId,
                AccountId = excluded.AccountId,
                AmountHint = excluded.AmountHint,
                MatchCount = excluded.MatchCount,
                CreatedAt = excluded.CreatedAt,
                UpdatedAt = excluded.UpdatedAt;
            """;
        AddParameter(command, "@Id", rule.Id.ToString());
        AddParameter(command, "@MatchText", rule.MatchText);
        AddParameter(command, "@NormalizedMatchText", rule.NormalizedMatchText);
        AddParameter(command, "@Type", rule.Type.ToString());
        AddParameter(command, "@CategoryId", rule.CategoryId.ToString());
        AddParameter(command, "@AccountId", SqliteData.ToDbValue(rule.AccountId));
        AddParameter(command, "@AmountHint", SqliteData.ToDbValue(rule.AmountHint));
        AddParameter(command, "@MatchCount", rule.MatchCount);
        AddParameter(command, "@CreatedAt", rule.CreatedAt.ToString("O"));
        AddParameter(command, "@UpdatedAt", rule.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CategoryLearningRules;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static CategoryLearningRule ReadRule(System.Data.Common.DbDataReader reader)
    {
        return new CategoryLearningRule(
            SqliteData.ReadGuid(reader, "Id"),
            SqliteData.ReadString(reader, "MatchText"),
            SqliteData.ReadString(reader, "NormalizedMatchText"),
            Enum.Parse<TransactionType>(SqliteData.ReadString(reader, "Type")),
            SqliteData.ReadGuid(reader, "CategoryId"),
            SqliteData.ReadNullableGuid(reader, "AccountId"),
            SqliteData.ReadNullableDecimal(reader, "AmountHint"),
            reader.GetInt32(reader.GetOrdinal("MatchCount")),
            DateTimeOffset.Parse(SqliteData.ReadString(reader, "CreatedAt"), System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(SqliteData.ReadString(reader, "UpdatedAt"), System.Globalization.CultureInfo.InvariantCulture));
    }
}
