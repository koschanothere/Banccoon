using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Database;

namespace Banccoon.Infrastructure.Repositories;

public sealed class SqliteScheduledOccurrenceOverrideRepository : SqliteRepositoryBase, IScheduledOccurrenceOverrideRepository
{
    public SqliteScheduledOccurrenceOverrideRepository(
        ISqliteConnectionFactory connectionFactory,
        IBanccoonDatabaseInitializer databaseInitializer)
        : base(connectionFactory, databaseInitializer)
    {
    }

    public async Task<IReadOnlyList<ScheduledOccurrenceOverride>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id,
                ScheduledTransactionId,
                OriginalOccurrenceDate,
                Kind,
                DelayedToDate
            FROM ScheduledOccurrenceOverrides;
            """;

        var overrides = new List<ScheduledOccurrenceOverride>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            overrides.Add(new ScheduledOccurrenceOverride(
                SqliteData.ReadGuid(reader, "Id"),
                SqliteData.ReadGuid(reader, "ScheduledTransactionId"),
                SqliteData.ReadDate(reader, "OriginalOccurrenceDate"),
                Enum.Parse<ScheduledOccurrenceOverrideKind>(SqliteData.ReadString(reader, "Kind")),
                SqliteData.ReadNullableDate(reader, "DelayedToDate")));
        }

        return overrides;
    }

    public async Task SaveAsync(ScheduledOccurrenceOverride occurrenceOverride, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ScheduledOccurrenceOverrides (
                Id,
                ScheduledTransactionId,
                OriginalOccurrenceDate,
                Kind,
                DelayedToDate
            )
            VALUES (
                @Id,
                @ScheduledTransactionId,
                @OriginalOccurrenceDate,
                @Kind,
                @DelayedToDate
            )
            ON CONFLICT(Id) DO UPDATE SET
                ScheduledTransactionId = excluded.ScheduledTransactionId,
                OriginalOccurrenceDate = excluded.OriginalOccurrenceDate,
                Kind = excluded.Kind,
                DelayedToDate = excluded.DelayedToDate;
            """;

        AddParameter(command, "@Id", occurrenceOverride.Id.ToString());
        AddParameter(command, "@ScheduledTransactionId", occurrenceOverride.ScheduledTransactionId.ToString());
        AddParameter(command, "@OriginalOccurrenceDate", SqliteData.DateToText(occurrenceOverride.OriginalOccurrenceDate));
        AddParameter(command, "@Kind", occurrenceOverride.Kind.ToString());
        AddParameter(command, "@DelayedToDate", SqliteData.ToDbValue(occurrenceOverride.DelayedToDate));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ScheduledOccurrenceOverrides WHERE Id = @Id;";
        AddParameter(command, "@Id", id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ScheduledOccurrenceOverrides;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
