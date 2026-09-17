using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Appearance;
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
            SELECT
                DefaultCurrency,
                DefaultForecastPeriod,
                ReminderFrequency,
                DateDisplayFormat,
                ThemeMode,
                AccentColor,
                NavigationStyle,
                ShowPowerUserFeatures,
                FreeToSpendWindowMode,
                FreeToSpendWindowDays,
                SafetyBuffer,
                MajorPaymentThreshold,
                ResolveUpcomingNearTermDays,
                PrimaryAccountId
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
            Enum.Parse<DateDisplayFormat>(SqliteData.ReadString(reader, "DateDisplayFormat")),
            Enum.Parse<AppThemeMode>(SqliteData.ReadString(reader, "ThemeMode")),
            Enum.Parse<AccentColor>(SqliteData.ReadString(reader, "AccentColor")),
            Enum.Parse<NavigationStyle>(SqliteData.ReadString(reader, "NavigationStyle")),
            SqliteData.ReadBoolean(reader, "ShowPowerUserFeatures"),
            Enum.Parse<FreeToSpendWindowMode>(SqliteData.ReadString(reader, "FreeToSpendWindowMode")),
            SqliteData.ReadInt32(reader, "FreeToSpendWindowDays"),
            SqliteData.ReadDecimal(reader, "SafetyBuffer"),
            SqliteData.ReadDecimal(reader, "MajorPaymentThreshold"),
            SqliteData.ReadInt32(reader, "ResolveUpcomingNearTermDays"),
            SqliteData.ReadNullableGuid(reader, "PrimaryAccountId"));
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Settings (
                Id,
                DefaultCurrency,
                DefaultForecastPeriod,
                ReminderFrequency,
                DateDisplayFormat,
                ThemeMode,
                AccentColor,
                NavigationStyle,
                ShowPowerUserFeatures,
                FreeToSpendWindowMode,
                FreeToSpendWindowDays,
                SafetyBuffer,
                MajorPaymentThreshold,
                ResolveUpcomingNearTermDays,
                PrimaryAccountId)
            VALUES (
                1,
                @DefaultCurrency,
                @DefaultForecastPeriod,
                @ReminderFrequency,
                @DateDisplayFormat,
                @ThemeMode,
                @AccentColor,
                @NavigationStyle,
                @ShowPowerUserFeatures,
                @FreeToSpendWindowMode,
                @FreeToSpendWindowDays,
                @SafetyBuffer,
                @MajorPaymentThreshold,
                @ResolveUpcomingNearTermDays,
                @PrimaryAccountId)
            ON CONFLICT(Id) DO UPDATE SET
                DefaultCurrency = excluded.DefaultCurrency,
                DefaultForecastPeriod = excluded.DefaultForecastPeriod,
                ReminderFrequency = excluded.ReminderFrequency,
                DateDisplayFormat = excluded.DateDisplayFormat,
                ThemeMode = excluded.ThemeMode,
                AccentColor = excluded.AccentColor,
                NavigationStyle = excluded.NavigationStyle,
                ShowPowerUserFeatures = excluded.ShowPowerUserFeatures,
                FreeToSpendWindowMode = excluded.FreeToSpendWindowMode,
                FreeToSpendWindowDays = excluded.FreeToSpendWindowDays,
                SafetyBuffer = excluded.SafetyBuffer,
                MajorPaymentThreshold = excluded.MajorPaymentThreshold,
                ResolveUpcomingNearTermDays = excluded.ResolveUpcomingNearTermDays,
                PrimaryAccountId = excluded.PrimaryAccountId;
            """;
        AddParameter(command, "@DefaultCurrency", settings.DefaultCurrency);
        AddParameter(command, "@DefaultForecastPeriod", settings.DefaultForecastPeriod.ToString());
        AddParameter(command, "@ReminderFrequency", settings.ReminderFrequency.ToString());
        AddParameter(command, "@DateDisplayFormat", settings.DateDisplayFormat.ToString());
        AddParameter(command, "@ThemeMode", settings.ThemeMode.ToString());
        AddParameter(command, "@AccentColor", settings.AccentColor.ToString());
        AddParameter(command, "@NavigationStyle", settings.NavigationStyle.ToString());
        AddParameter(command, "@ShowPowerUserFeatures", settings.ShowPowerUserFeatures ? 1 : 0);
        AddParameter(command, "@FreeToSpendWindowMode", settings.FreeToSpendWindowMode.ToString());
        AddParameter(command, "@FreeToSpendWindowDays", settings.FreeToSpendWindowDays);
        AddParameter(command, "@SafetyBuffer", SqliteData.DecimalToText(settings.SafetyBuffer));
        AddParameter(command, "@MajorPaymentThreshold", SqliteData.DecimalToText(settings.MajorPaymentThreshold));
        AddParameter(command, "@ResolveUpcomingNearTermDays", settings.ResolveUpcomingNearTermDays);
        AddParameter(command, "@PrimaryAccountId", SqliteData.ToDbValue(settings.PrimaryAccountId));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
