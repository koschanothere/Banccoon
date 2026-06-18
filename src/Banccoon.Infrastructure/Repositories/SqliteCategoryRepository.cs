using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Database;

namespace Banccoon.Infrastructure.Repositories;

public sealed class SqliteCategoryRepository : SqliteRepositoryBase, ICategoryRepository
{
    public SqliteCategoryRepository(
        ISqliteConnectionFactory connectionFactory,
        IBanccoonDatabaseInitializer databaseInitializer)
        : base(connectionFactory, databaseInitializer)
    {
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Type FROM Categories ORDER BY Name;";

        var categories = new List<Category>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(ReadCategory(reader));
        }

        return categories;
    }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Type FROM Categories WHERE Id = @Id;";
        AddParameter(command, "@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCategory(reader) : null;
    }

    public async Task SaveAsync(Category category, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Categories (Id, Name, Type)
            VALUES (@Id, @Name, @Type)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                Type = excluded.Type;
            """;
        AddParameter(command, "@Id", category.Id.ToString());
        AddParameter(command, "@Name", category.Name);
        AddParameter(command, "@Type", SqliteData.ToDbValue(category.Type?.ToString()));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Categories WHERE Id = @Id;";
        AddParameter(command, "@Id", id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Categories;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Category ReadCategory(System.Data.Common.DbDataReader reader)
    {
        return new Category(
            SqliteData.ReadGuid(reader, "Id"),
            SqliteData.ReadString(reader, "Name"),
            SqliteData.ReadNullableString(reader, "Type") is { } type
                ? Enum.Parse<TransactionType>(type)
                : null);
    }
}
