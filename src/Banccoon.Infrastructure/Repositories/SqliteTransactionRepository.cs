using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Database;

namespace Banccoon.Infrastructure.Repositories;

public sealed class SqliteTransactionRepository : SqliteRepositoryBase, ITransactionRepository
{
    public SqliteTransactionRepository(
        ISqliteConnectionFactory connectionFactory,
        IBanccoonDatabaseInitializer databaseInitializer)
        : base(connectionFactory, databaseInitializer)
    {
    }

    public async Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Date, Amount, AccountId, CategoryId, Notes, Type
            FROM Transactions
            ORDER BY Date DESC;
            """;

        return await ReadTransactionsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Date, Amount, AccountId, CategoryId, Notes, Type
            FROM Transactions
            WHERE AccountId = @AccountId
            ORDER BY Date DESC;
            """;
        AddParameter(command, "@AccountId", accountId.ToString());

        return await ReadTransactionsAsync(command, cancellationToken);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Date, Amount, AccountId, CategoryId, Notes, Type
            FROM Transactions
            WHERE Id = @Id;
            """;
        AddParameter(command, "@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTransaction(reader) : null;
    }

    public async Task SaveAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Transactions (Id, Date, Amount, AccountId, CategoryId, Notes, Type)
            VALUES (@Id, @Date, @Amount, @AccountId, @CategoryId, @Notes, @Type)
            ON CONFLICT(Id) DO UPDATE SET
                Date = excluded.Date,
                Amount = excluded.Amount,
                AccountId = excluded.AccountId,
                CategoryId = excluded.CategoryId,
                Notes = excluded.Notes,
                Type = excluded.Type;
            """;
        AddTransactionParameters(command, transaction);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Transactions WHERE Id = @Id;";
        AddParameter(command, "@Id", id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Transactions;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<Transaction>> ReadTransactionsAsync(
        Microsoft.Data.Sqlite.SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var transactions = new List<Transaction>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            transactions.Add(ReadTransaction(reader));
        }

        return transactions;
    }

    private static void AddTransactionParameters(Microsoft.Data.Sqlite.SqliteCommand command, Transaction transaction)
    {
        AddParameter(command, "@Id", transaction.Id.ToString());
        AddParameter(command, "@Date", SqliteData.DateToText(transaction.Date));
        AddParameter(command, "@Amount", SqliteData.DecimalToText(transaction.Amount));
        AddParameter(command, "@AccountId", transaction.AccountId.ToString());
        AddParameter(command, "@CategoryId", SqliteData.ToDbValue(transaction.CategoryId));
        AddParameter(command, "@Notes", SqliteData.ToDbValue(transaction.Notes));
        AddParameter(command, "@Type", transaction.Type.ToString());
    }

    private static Transaction ReadTransaction(System.Data.Common.DbDataReader reader)
    {
        return new Transaction(
            SqliteData.ReadGuid(reader, "Id"),
            SqliteData.ReadDate(reader, "Date"),
            SqliteData.ReadDecimal(reader, "Amount"),
            SqliteData.ReadGuid(reader, "AccountId"),
            SqliteData.ReadNullableGuid(reader, "CategoryId"),
            SqliteData.ReadNullableString(reader, "Notes"),
            Enum.Parse<TransactionType>(SqliteData.ReadString(reader, "Type")));
    }
}
