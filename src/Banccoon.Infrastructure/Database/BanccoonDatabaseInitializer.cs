using Microsoft.Data.Sqlite;

namespace Banccoon.Infrastructure.Database;

public sealed class BanccoonDatabaseInitializer : IBanccoonDatabaseInitializer
{
    private readonly ISqliteConnectionFactory connectionFactory;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private bool isInitialized;

    public BanccoonDatabaseInitializer(ISqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    // Every repository call goes through this before touching the database, so without caching,
    // rapid navigation (which fans out into many repository calls at once, e.g. the sidebar's
    // favorites refresh plus a page's own InitializeAsync) was reopening a connection and re-running
    // the entire schema/migration battery on every single call - needless connection churn that
    // made transient SQLite lock contention far more likely. The underlying SQL is already
    // idempotent (CREATE TABLE IF NOT EXISTS / AddMissingColumnAsync), so running it once per
    // process instead of once per call changes nothing except how much redundant work happens.
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (isInitialized)
        {
            return;
        }

        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (isInitialized)
            {
                return;
            }

            await InitializeCoreAsync(cancellationToken);
            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;

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
                CardLastFourDigits TEXT NULL,
                PlanningValue TEXT NULL,
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Type TEXT NULL,
                Color TEXT NULL
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
                Time TEXT NULL,
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE CASCADE,
                FOREIGN KEY (DestinationAccountId) REFERENCES Accounts(Id) ON DELETE SET NULL,
                FOREIGN KEY (DestinationGoalId) REFERENCES SavingsGoals(Id) ON DELETE SET NULL,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE SET NULL,
                FOREIGN KEY (PaidScheduledTransactionId) REFERENCES ScheduledTransactions(Id) ON DELETE SET NULL
            );

            -- Pages read transactions by date range (only the last 12 months are kept in memory).
            CREATE INDEX IF NOT EXISTS IX_Transactions_Date ON Transactions(Date);

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
                ClosingBalance TEXT NULL,
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
                Time TEXT NULL,
                DestinationAccountId TEXT NULL,
                IsIncoming INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (BatchId) REFERENCES StatementImportBatches(Id) ON DELETE CASCADE,
                FOREIGN KEY (DestinationAccountId) REFERENCES Accounts(Id) ON DELETE SET NULL,
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
                DestinationAccountId TEXT NULL,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE,
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE CASCADE,
                FOREIGN KEY (DestinationAccountId) REFERENCES Accounts(Id) ON DELETE CASCADE
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

            CREATE TABLE IF NOT EXISTS ScheduledOccurrenceOverrides (
                Id TEXT PRIMARY KEY,
                ScheduledTransactionId TEXT NOT NULL,
                OriginalOccurrenceDate TEXT NOT NULL,
                Kind TEXT NOT NULL,
                DelayedToDate TEXT NULL,
                FOREIGN KEY (ScheduledTransactionId) REFERENCES ScheduledTransactions(Id) ON DELETE CASCADE
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

            -- A bank's own operation categories as its statements show them, each linked to an app
            -- category or (CategoryId NULL) skipped. See BankCategoryLink.
            CREATE TABLE IF NOT EXISTS BankCategoryLinks (
                ParserId TEXT NOT NULL,
                BankCategory TEXT NOT NULL COLLATE NOCASE,
                CategoryId TEXT NULL,
                FirstSeenAt TEXT NOT NULL,
                PRIMARY KEY (ParserId, BankCategory),
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE SET NULL
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
                ShowPowerUserFeatures INTEGER NOT NULL DEFAULT 0,
                FreeToSpendWindowMode TEXT NOT NULL DEFAULT 'RollingDays',
                FreeToSpendWindowDays INTEGER NOT NULL DEFAULT 7,
                SafetyBuffer TEXT NOT NULL DEFAULT '0',
                MajorPaymentThreshold TEXT NOT NULL DEFAULT '0',
                ResolveUpcomingNearTermDays INTEGER NOT NULL DEFAULT 3,
                PrimaryAccountId TEXT NULL,
                DashboardSectionOrder TEXT NOT NULL DEFAULT 'Upcoming,Analytics,Goals',
                DisplayLanguage TEXT NOT NULL DEFAULT 'en',
                DefaultAccountType TEXT NOT NULL DEFAULT 'DebitCard',
                PrivacyModeEnabled INTEGER NOT NULL DEFAULT 0,
                AppLockPinHash TEXT NULL,
                AppLockPinSalt TEXT NULL,
                AppLockAutoLockMinutes INTEGER NOT NULL DEFAULT 5,
                AutoBackupEnabled INTEGER NOT NULL DEFAULT 0,
                AutoBackupFrequencyDays INTEGER NOT NULL DEFAULT 30,
                AutoBackupRetentionCount INTEGER NOT NULL DEFAULT 5,
                LastAutoBackupAt TEXT NULL,
                DashboardPrimaryMetric TEXT NOT NULL DEFAULT 'FreeToSpend',
                LegacySavingsGoalsConverted INTEGER NOT NULL DEFAULT 0
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
            "Categories",
            "Color",
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
            "Accounts",
            "PlanningValue",
            "TEXT NULL",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Accounts",
            "IsFavorite",
            "INTEGER NOT NULL DEFAULT 0",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Accounts",
            "SortOrder",
            "INTEGER NOT NULL DEFAULT 0",
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
        await AddMissingColumnAsync(
            connection,
            "Settings",
            "FreeToSpendWindowMode",
            "TEXT NOT NULL DEFAULT 'RollingDays'",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Settings",
            "FreeToSpendWindowDays",
            "INTEGER NOT NULL DEFAULT 7",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Settings",
            "SafetyBuffer",
            "TEXT NOT NULL DEFAULT '0'",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "Settings",
            "MajorPaymentThreshold",
            "TEXT NOT NULL DEFAULT '0'",
            cancellationToken);

        await AddMissingColumnAsync(
            connection,
            "Settings",
            "ResolveUpcomingNearTermDays",
            "INTEGER NOT NULL DEFAULT 3",
            cancellationToken);

        await AddMissingColumnAsync(
            connection,
            "Settings",
            "PrimaryAccountId",
            "TEXT NULL",
            cancellationToken);

        await AddMissingColumnAsync(
            connection,
            "Transactions",
            "Time",
            "TEXT NULL",
            cancellationToken);

        await AddMissingColumnAsync(
            connection,
            "StatementImportRows",
            "Time",
            "TEXT NULL",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "StatementImportRows",
            "DestinationAccountId",
            "TEXT NULL",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "StatementImportRows",
            "IsIncoming",
            "INTEGER NOT NULL DEFAULT 0",
            cancellationToken);
        await AddMissingColumnAsync(
            connection,
            "StatementImportRows",
            "BankCategory",
            "TEXT NULL",
            cancellationToken);

        await AddMissingColumnAsync(
            connection,
            "CategoryLearningRules",
            "DestinationAccountId",
            "TEXT NULL",
            cancellationToken);

        await AddMissingColumnAsync(
            connection,
            "StatementImportBatches",
            "ClosingBalance",
            "TEXT NULL",
            cancellationToken);

        await AddMissingColumnAsync(
            connection,
            "Settings",
            "DashboardSectionOrder",
            "TEXT NOT NULL DEFAULT 'Upcoming,Analytics,Goals'",
            cancellationToken);

        await AddMissingColumnAsync(connection, "Settings", "DisplayLanguage", "TEXT NOT NULL DEFAULT 'en'", cancellationToken);
        await AddMissingColumnAsync(connection, "Settings", "DefaultAccountType", "TEXT NOT NULL DEFAULT 'DebitCard'", cancellationToken);
        await AddMissingColumnAsync(connection, "Settings", "PrivacyModeEnabled", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddMissingColumnAsync(connection, "Settings", "AppLockPinHash", "TEXT NULL", cancellationToken);
        await AddMissingColumnAsync(connection, "Settings", "AppLockPinSalt", "TEXT NULL", cancellationToken);
        await AddMissingColumnAsync(connection, "Settings", "AppLockAutoLockMinutes", "INTEGER NOT NULL DEFAULT 5", cancellationToken);
        await AddMissingColumnAsync(connection, "Settings", "AutoBackupEnabled", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddMissingColumnAsync(connection, "Settings", "AutoBackupFrequencyDays", "INTEGER NOT NULL DEFAULT 30", cancellationToken);
        await AddMissingColumnAsync(connection, "Settings", "AutoBackupRetentionCount", "INTEGER NOT NULL DEFAULT 5", cancellationToken);
        await AddMissingColumnAsync(connection, "Settings", "LastAutoBackupAt", "TEXT NULL", cancellationToken);
        await AddMissingColumnAsync(connection, "Settings", "DashboardPrimaryMetric", "TEXT NOT NULL DEFAULT 'FreeToSpend'", cancellationToken);
        await AddMissingColumnAsync(connection, "Settings", "LegacySavingsGoalsConverted", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
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
