using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Database;

namespace Banccoon.Infrastructure.Repositories;

public sealed class SqliteSettingsRepository : SqliteRepositoryBase, ISettingsRepository
{
    private static readonly AppSettings DefaultSettings = new(
        "EUR",
        ForecastPeriod.ThirtyDays,
        ReminderFrequency.Weekly,
        DateDisplayFormat.DayMonthYear);

    public SqliteSettingsRepository(
        ISqliteConnectionFactory connectionFactory,
        IBanccoonDatabaseInitializer databaseInitializer)
        : base(connectionFactory, databaseInitializer)
    {
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DefaultCurrency, DefaultForecastPeriod, ReminderFrequency, DateDisplayFormat
            FROM Settings
            WHERE Id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return DefaultSettings;
        }

        return new AppSettings(
            SqliteData.ReadString(reader, "DefaultCurrency"),
            Enum.Parse<ForecastPeriod>(SqliteData.ReadString(reader, "DefaultForecastPeriod")),
            Enum.Parse<ReminderFrequency>(SqliteData.ReadString(reader, "ReminderFrequency")),
            Enum.Parse<DateDisplayFormat>(SqliteData.ReadString(reader, "DateDisplayFormat")));
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Settings (Id, DefaultCurrency, DefaultForecastPeriod, ReminderFrequency, DateDisplayFormat)
            VALUES (1, @DefaultCurrency, @DefaultForecastPeriod, @ReminderFrequency, @DateDisplayFormat)
            ON CONFLICT(Id) DO UPDATE SET
                DefaultCurrency = excluded.DefaultCurrency,
                DefaultForecastPeriod = excluded.DefaultForecastPeriod,
                ReminderFrequency = excluded.ReminderFrequency,
                DateDisplayFormat = excluded.DateDisplayFormat;
            """;
        AddParameter(command, "@DefaultCurrency", settings.DefaultCurrency);
        AddParameter(command, "@DefaultForecastPeriod", settings.DefaultForecastPeriod.ToString());
        AddParameter(command, "@ReminderFrequency", settings.ReminderFrequency.ToString());
        AddParameter(command, "@DateDisplayFormat", settings.DateDisplayFormat.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
