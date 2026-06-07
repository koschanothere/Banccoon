using Banccoon.Core.Models;
using Banccoon.Core.Recurrence;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Database;

namespace Banccoon.Infrastructure.Repositories;

public sealed class SqliteScheduledTransactionRepository : SqliteRepositoryBase, IScheduledTransactionRepository
{
    public SqliteScheduledTransactionRepository(
        ISqliteConnectionFactory connectionFactory,
        IBanccoonDatabaseInitializer databaseInitializer)
        : base(connectionFactory, databaseInitializer)
    {
    }

    public async Task<IReadOnlyList<ScheduledTransaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id,
                Name,
                Amount,
                AccountId,
                CategoryId,
                Type,
                RecurrenceFrequency,
                RecurrenceInterval,
                RecurrenceStartDate,
                RecurrenceEndDate,
                RecurrenceDayOfWeek,
                RecurrenceDayOfMonth,
                RecurrenceMonthlyMode,
                NextOccurrence,
                Active
            FROM ScheduledTransactions
            ORDER BY NextOccurrence, Name;
            """;

        var scheduledTransactions = new List<ScheduledTransaction>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            scheduledTransactions.Add(ReadScheduledTransaction(reader));
        }

        return scheduledTransactions;
    }

    public async Task<ScheduledTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id,
                Name,
                Amount,
                AccountId,
                CategoryId,
                Type,
                RecurrenceFrequency,
                RecurrenceInterval,
                RecurrenceStartDate,
                RecurrenceEndDate,
                RecurrenceDayOfWeek,
                RecurrenceDayOfMonth,
                RecurrenceMonthlyMode,
                NextOccurrence,
                Active
            FROM ScheduledTransactions
            WHERE Id = @Id;
            """;
        AddParameter(command, "@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadScheduledTransaction(reader) : null;
    }

    public async Task SaveAsync(ScheduledTransaction scheduledTransaction, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ScheduledTransactions (
                Id,
                Name,
                Amount,
                AccountId,
                CategoryId,
                Type,
                RecurrenceFrequency,
                RecurrenceInterval,
                RecurrenceStartDate,
                RecurrenceEndDate,
                RecurrenceDayOfWeek,
                RecurrenceDayOfMonth,
                RecurrenceMonthlyMode,
                NextOccurrence,
                Active
            )
            VALUES (
                @Id,
                @Name,
                @Amount,
                @AccountId,
                @CategoryId,
                @Type,
                @RecurrenceFrequency,
                @RecurrenceInterval,
                @RecurrenceStartDate,
                @RecurrenceEndDate,
                @RecurrenceDayOfWeek,
                @RecurrenceDayOfMonth,
                @RecurrenceMonthlyMode,
                @NextOccurrence,
                @Active
            )
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                Amount = excluded.Amount,
                AccountId = excluded.AccountId,
                CategoryId = excluded.CategoryId,
                Type = excluded.Type,
                RecurrenceFrequency = excluded.RecurrenceFrequency,
                RecurrenceInterval = excluded.RecurrenceInterval,
                RecurrenceStartDate = excluded.RecurrenceStartDate,
                RecurrenceEndDate = excluded.RecurrenceEndDate,
                RecurrenceDayOfWeek = excluded.RecurrenceDayOfWeek,
                RecurrenceDayOfMonth = excluded.RecurrenceDayOfMonth,
                RecurrenceMonthlyMode = excluded.RecurrenceMonthlyMode,
                NextOccurrence = excluded.NextOccurrence,
                Active = excluded.Active;
            """;

        AddParameter(command, "@Id", scheduledTransaction.Id.ToString());
        AddParameter(command, "@Name", scheduledTransaction.Name);
        AddParameter(command, "@Amount", SqliteData.DecimalToText(scheduledTransaction.Amount));
        AddParameter(command, "@AccountId", scheduledTransaction.AccountId.ToString());
        AddParameter(command, "@CategoryId", SqliteData.ToDbValue(scheduledTransaction.CategoryId));
        AddParameter(command, "@Type", scheduledTransaction.Type.ToString());
        AddParameter(command, "@RecurrenceFrequency", scheduledTransaction.RecurrenceRule.Frequency.ToString());
        AddParameter(command, "@RecurrenceInterval", scheduledTransaction.RecurrenceRule.Interval);
        AddParameter(command, "@RecurrenceStartDate", SqliteData.DateToText(scheduledTransaction.RecurrenceRule.StartDate));
        AddParameter(command, "@RecurrenceEndDate", SqliteData.ToDbValue(scheduledTransaction.RecurrenceRule.EndDate));
        AddParameter(command, "@RecurrenceDayOfWeek", SqliteData.ToDbValue(scheduledTransaction.RecurrenceRule.DayOfWeek?.ToString()));
        AddParameter(command, "@RecurrenceDayOfMonth", scheduledTransaction.RecurrenceRule.DayOfMonth ?? (object)DBNull.Value);
        AddParameter(command, "@RecurrenceMonthlyMode", scheduledTransaction.RecurrenceRule.MonthlyMode.ToString());
        AddParameter(command, "@NextOccurrence", SqliteData.DateToText(scheduledTransaction.NextOccurrence));
        AddParameter(command, "@Active", scheduledTransaction.Active ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ScheduledTransactions WHERE Id = @Id;";
        AddParameter(command, "@Id", id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ScheduledTransactions;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ScheduledTransaction ReadScheduledTransaction(System.Data.Common.DbDataReader reader)
    {
        var recurrenceDayOfWeek = SqliteData.ReadNullableString(reader, "RecurrenceDayOfWeek");

        var recurrenceRule = new RecurrenceRule(
            Enum.Parse<RecurrenceFrequency>(SqliteData.ReadString(reader, "RecurrenceFrequency")),
            reader.GetInt32(reader.GetOrdinal("RecurrenceInterval")),
            SqliteData.ReadDate(reader, "RecurrenceStartDate"),
            SqliteData.ReadNullableDate(reader, "RecurrenceEndDate"),
            recurrenceDayOfWeek is null ? null : Enum.Parse<DayOfWeek>(recurrenceDayOfWeek),
            SqliteData.ReadNullableInt32(reader, "RecurrenceDayOfMonth"),
            Enum.Parse<MonthlyRecurrenceMode>(SqliteData.ReadString(reader, "RecurrenceMonthlyMode")));

        return new ScheduledTransaction(
            SqliteData.ReadGuid(reader, "Id"),
            SqliteData.ReadString(reader, "Name"),
            SqliteData.ReadDecimal(reader, "Amount"),
            SqliteData.ReadGuid(reader, "AccountId"),
            SqliteData.ReadNullableGuid(reader, "CategoryId"),
            Enum.Parse<TransactionType>(SqliteData.ReadString(reader, "Type")),
            recurrenceRule,
            SqliteData.ReadDate(reader, "NextOccurrence"),
            SqliteData.ReadBoolean(reader, "Active"));
    }
}
