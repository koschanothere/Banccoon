using System.Globalization;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;
using Banccoon.Infrastructure.Database;

namespace Banccoon.Infrastructure.Repositories;

// Not cached: a handful of rows per bank, read once per import (and per approval's suggestion
// refresh), and its CategoryId is cleared by the database itself when a category is deleted
// (ON DELETE SET NULL), which a cache wouldn't see.
public sealed class SqliteBankCategoryLinkRepository : SqliteRepositoryBase, IBankCategoryLinkRepository
{
    public SqliteBankCategoryLinkRepository(
        ISqliteConnectionFactory connectionFactory,
        IBanccoonDatabaseInitializer databaseInitializer)
        : base(connectionFactory, databaseInitializer)
    {
    }

    public async Task<IReadOnlyList<BankCategoryLink>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ParserId, BankCategory, CategoryId, FirstSeenAt
            FROM BankCategoryLinks
            ORDER BY ParserId, BankCategory;
            """;

        return await ReadLinksAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<BankCategoryLink>> GetByParserAsync(string parserId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ParserId, BankCategory, CategoryId, FirstSeenAt
            FROM BankCategoryLinks
            WHERE ParserId = @ParserId
            ORDER BY BankCategory;
            """;
        AddParameter(command, "@ParserId", parserId);

        return await ReadLinksAsync(command, cancellationToken);
    }

    public async Task SaveAllAsync(IReadOnlyList<BankCategoryLink> links, CancellationToken cancellationToken = default)
    {
        if (links.Count == 0)
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var link in links)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO BankCategoryLinks (ParserId, BankCategory, CategoryId, FirstSeenAt)
                VALUES (@ParserId, @BankCategory, @CategoryId, @FirstSeenAt)
                ON CONFLICT(ParserId, BankCategory) DO UPDATE SET
                    CategoryId = excluded.CategoryId;
                """;
            AddParameter(command, "@ParserId", link.ParserId);
            AddParameter(command, "@BankCategory", link.BankCategory);
            AddParameter(command, "@CategoryId", SqliteData.ToDbValue(link.CategoryId));
            AddParameter(command, "@FirstSeenAt", link.FirstSeenAt.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReassignCategoryAsync(Guid fromCategoryId, Guid toCategoryId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BankCategoryLinks SET CategoryId = @To WHERE CategoryId = @From;";
        AddParameter(command, "@From", fromCategoryId.ToString());
        AddParameter(command, "@To", toCategoryId.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM BankCategoryLinks;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<BankCategoryLink>> ReadLinksAsync(
        Microsoft.Data.Sqlite.SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var links = new List<BankCategoryLink>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            links.Add(new BankCategoryLink(
                SqliteData.ReadString(reader, "ParserId"),
                SqliteData.ReadString(reader, "BankCategory"),
                SqliteData.ReadNullableGuid(reader, "CategoryId"),
                DateTimeOffset.Parse(SqliteData.ReadString(reader, "FirstSeenAt"), CultureInfo.InvariantCulture)));
        }

        return links;
    }
}
