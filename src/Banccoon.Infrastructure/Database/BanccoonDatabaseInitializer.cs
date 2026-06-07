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
                PlannedPaymentAmount TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Transactions (
                Id TEXT PRIMARY KEY,
                Date TEXT NOT NULL,
                Amount TEXT NOT NULL,
                AccountId TEXT NOT NULL,
                CategoryId TEXT NULL,
                Notes TEXT NULL,
                Type TEXT NOT NULL,
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE CASCADE,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE SET NULL
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
                ReminderFrequency TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
