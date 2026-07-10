using Microsoft.Data.Sqlite;

namespace Banccoon.Infrastructure.Database;

public sealed class BanccoonDatabaseInitializer : IBanccoonDatabaseInitializer
{
    private readonly ISqliteConnectionFactory connectionFactory;

    public BanccoonDatabaseInitializer(ISqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS Accounts (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Type TEXT NOT NULL,
                CurrentBalance TEXT NOT NULL,
                Currency TEXT NOT NULL,
                CreatedDate TEXT NOT NULL,
                IsArchived INTEGER NOT NULL,
                CreditCardCurrentDebt TEXT NULL,
                StatementDayOfMonth INTEGER NULL,
                PaymentDueDayOfMonth INTEGER NULL,
                MinimumPayment TEXT NULL,
                PlannedPaymentAmount TEXT NULL,
                IncludeInDashboardTotals INTEGER NOT NULL DEFAULT 1,
                AccountNumber TEXT NULL,
                CardLastFourDigits TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Type TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS Transactions (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL DEFAULT '',
                Date TEXT NOT NULL,
                Amount TEXT NOT NULL,
                AccountId TEXT NOT NULL,
                DestinationAccountId TEXT NULL,
                DestinationGoalId TEXT NULL,
                CategoryId TEXT NULL,
                Notes TEXT NULL,
                Type TEXT NOT NULL,
                PaidScheduledTransactionId TEXT NULL,
                PaidScheduledOccurrenceDate TEXT NULL,
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE CASCADE,
                FOREIGN KEY (DestinationAccountId) REFERENCES Accounts(Id) ON DELETE SET NULL,
                FOREIGN KEY (DestinationGoalId) REFERENCES SavingsGoals(Id) ON DELETE SET NULL,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE SET NULL,
                FOREIGN KEY (PaidScheduledTransactionId) REFERENCES ScheduledTransactions(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS StatementImportBatches (
                Id TEXT PRIMARY KEY,
                AccountId TEXT NOT NULL,
                ParserId TEXT NOT NULL,
                ParserName TEXT NOT NULL,
                SourceFileName TEXT NOT NULL,
                SourceFilePath TEXT NULL,
                ImportedAt TEXT NOT NULL,
                Status TEXT NOT NULL,
                RowCount INTEGER NOT NULL,
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS StatementImportRows (
                Id TEXT PRIMARY KEY,
                BatchId TEXT NOT NULL,
                Date TEXT NOT NULL,
                Amount TEXT NOT NULL,
                Type TEXT NOT NULL,
                Description TEXT NOT NULL,
                NormalizedDescription TEXT NOT NULL,
                Counterparty TEXT NULL,
                ExternalReference TEXT NULL,
                RawText TEXT NULL,
                SuggestedCategoryId TEXT NULL,
                CategoryId TEXT NULL,
                Status TEXT NOT NULL,
                IsDuplicate INTEGER NOT NULL,
                DuplicateTransactionId TEXT NULL,
                CreatedTransactionId TEXT NULL,
                FOREIGN KEY (BatchId) REFERENCES StatementImportBatches(Id) ON DELETE CASCADE,
                FOREIGN KEY (SuggestedCategoryId) REFERENCES Categories(Id) ON DELETE SET NULL,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE SET NULL,
                FOREIGN KEY (DuplicateTransactionId) REFERENCES Transactions(Id) ON DELETE SET NULL,
                FOREIGN KEY (CreatedTransactionId) REFERENCES Transactions(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS CategoryLearningRules (
                Id TEXT PRIMARY KEY,
                MatchText TEXT NOT NULL,
                NormalizedMatchText TEXT NOT NULL,
                Type TEXT NOT NULL,
                CategoryId TEXT NOT NULL,
                AccountId TEXT NULL,
                AmountHint TEXT NULL,
                MatchCount INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE,
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ScheduledTransactions (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Amount TEXT NOT NULL,
                AccountId TEXT NOT NULL,
                CategoryId TEXT NULL,
                Type TEXT NOT NULL,
                RecurrenceFrequency TEXT NOT NULL,
                RecurrenceInterval INTEGER NOT NULL,
                RecurrenceStartDate TEXT NOT NULL,
                RecurrenceEndDate TEXT NULL,
                RecurrenceDayOfWeek TEXT NULL,
                RecurrenceDayOfMonth INTEGER NULL,
                RecurrenceMonthlyMode TEXT NOT NULL,
                NextOccurrence TEXT NOT NULL,
                Active INTEGER NOT NULL,
                Notes TEXT NULL,
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE CASCADE,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS SavingsGoals (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                TargetAmount TEXT NOT NULL,
                CurrentAmount TEXT NOT NULL,
                TargetDate TEXT NULL,
                AccountId TEXT NULL,
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS Settings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                DefaultCurrency TEXT NOT NULL,
                DefaultForecastPeriod TEXT NOT NULL,
                ReminderFrequency TEXT NOT NULL,
                DateDisplayFormat TEXT NOT NULL DEFAULT 'DayMonthYear',
                ThemeMode TEXT NOT NULL DEFAULT 'Light',
                AccentColor TEXT NOT NULL DEFAULT 'Emerald',
                NavigationStyle TEXT NOT NULL DEFAULT 'Rail',
                ShowPowerUserFeatures INTEGER NOT NULL DEFAULT 0
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Categories",
            "Type",
            "TEXT NULL",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Transactions",
            "Name",
            "TEXT NOT NULL DEFAULT ''",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Transactions",
            "DestinationAccountId",
            "TEXT NULL",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Transactions",
            "DestinationGoalId",
            "TEXT NULL",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Transactions",
            "PaidScheduledTransactionId",
            "TEXT NULL",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Transactions",
            "PaidScheduledOccurrenceDate",
            "TEXT NULL",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "ScheduledTransactions",
            "Notes",
            "TEXT NULL",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Accounts",
            "IncludeInDashboardTotals",
            "INTEGER NOT NULL DEFAULT 1",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Accounts",
            "AccountNumber",
            "TEXT NULL",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Accounts",
            "CardLastFourDigits",
            "TEXT NULL",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Settings",
            "DateDisplayFormat",
            "TEXT NOT NULL DEFAULT 'DayMonthYear'",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Settings",
            "ThemeMode",
            "TEXT NOT NULL DEFAULT 'Light'",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Settings",
            "AccentColor",
            "TEXT NOT NULL DEFAULT 'Emerald'",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Settings",
            "NavigationStyle",
            "TEXT NOT NULL DEFAULT 'Rail'",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Settings",
            "ShowPowerUserFeatures",
            "INTEGER NOT NULL DEFAULT 0",
            cancellationToken);
    }

    private static async Task AddMissingColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using var queryCommand = connection.CreateCommand();
        queryCommand.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await queryCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
