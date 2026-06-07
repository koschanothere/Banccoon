using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Database;

namespace Banccoon.Infrastructure.Repositories;

public sealed class SqliteSavingsGoalRepository : SqliteRepositoryBase, ISavingsGoalRepository
{
    public SqliteSavingsGoalRepository(
        ISqliteConnectionFactory connectionFactory,
        IBanccoonDatabaseInitializer databaseInitializer)
        : base(connectionFactory, databaseInitializer)
    {
    }

    public async Task<IReadOnlyList<SavingsGoal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, TargetAmount, CurrentAmount, TargetDate, AccountId
            FROM SavingsGoals
            ORDER BY Name;
            """;

        var goals = new List<SavingsGoal>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            goals.Add(ReadSavingsGoal(reader));
        }

        return goals;
    }

    public async Task<SavingsGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, TargetAmount, CurrentAmount, TargetDate, AccountId
            FROM SavingsGoals
            WHERE Id = @Id;
            """;
        AddParameter(command, "@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSavingsGoal(reader) : null;
    }

    public async Task SaveAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SavingsGoals (Id, Name, TargetAmount, CurrentAmount, TargetDate, AccountId)
            VALUES (@Id, @Name, @TargetAmount, @CurrentAmount, @TargetDate, @AccountId)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                TargetAmount = excluded.TargetAmount,
                CurrentAmount = excluded.CurrentAmount,
                TargetDate = excluded.TargetDate,
                AccountId = excluded.AccountId;
            """;
        AddParameter(command, "@Id", savingsGoal.Id.ToString());
        AddParameter(command, "@Name", savingsGoal.Name);
        AddParameter(command, "@TargetAmount", SqliteData.DecimalToText(savingsGoal.TargetAmount));
        AddParameter(command, "@CurrentAmount", SqliteData.DecimalToText(savingsGoal.CurrentAmount));
        AddParameter(command, "@TargetDate", SqliteData.ToDbValue(savingsGoal.TargetDate));
        AddParameter(command, "@AccountId", SqliteData.ToDbValue(savingsGoal.AccountId));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SavingsGoals WHERE Id = @Id;";
        AddParameter(command, "@Id", id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SavingsGoal ReadSavingsGoal(System.Data.Common.DbDataReader reader)
    {
        return new SavingsGoal(
            SqliteData.ReadGuid(reader, "Id"),
            SqliteData.ReadString(reader, "Name"),
            SqliteData.ReadDecimal(reader, "TargetAmount"),
            SqliteData.ReadDecimal(reader, "CurrentAmount"),
            SqliteData.ReadNullableDate(reader, "TargetDate"),
            SqliteData.ReadNullableGuid(reader, "AccountId"));
    }
}
