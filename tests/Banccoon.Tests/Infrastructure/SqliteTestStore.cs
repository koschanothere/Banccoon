using Banccoon.Infrastructure.Database;
using Banccoon.Infrastructure.Repositories;

namespace Banccoon.Tests.Infrastructure;

public sealed class SqliteTestStore : IAsyncDisposable
{
    private readonly string databasePath;

    public SqliteTestStore()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "Banccoon.Tests");
        Directory.CreateDirectory(testDirectory);
        databasePath = Path.Combine(testDirectory, $"{Guid.NewGuid():N}.db");

        var pathProvider = new StaticDatabasePathProvider(databasePath);
        ConnectionFactory = new SqliteConnectionFactory(pathProvider);
        Initializer = new BanccoonDatabaseInitializer(ConnectionFactory);

        Accounts = new SqliteAccountRepository(ConnectionFactory, Initializer);
        Categories = new SqliteCategoryRepository(ConnectionFactory, Initializer);
        Transactions = new SqliteTransactionRepository(ConnectionFactory, Initializer);
        ScheduledTransactions = new SqliteScheduledTransactionRepository(ConnectionFactory, Initializer);
        SavingsGoals = new SqliteSavingsGoalRepository(ConnectionFactory, Initializer);
        Settings = new SqliteSettingsRepository(ConnectionFactory, Initializer);
        StatementImports = new SqliteStatementImportRepository(ConnectionFactory, Initializer);
        CategoryLearningRules = new SqliteCategoryLearningRuleRepository(ConnectionFactory, Initializer);
    }

    public SqliteConnectionFactory ConnectionFactory { get; }

    public BanccoonDatabaseInitializer Initializer { get; }

    public SqliteAccountRepository Accounts { get; }

    public SqliteCategoryRepository Categories { get; }

    public SqliteTransactionRepository Transactions { get; }

    public SqliteScheduledTransactionRepository ScheduledTransactions { get; }

    public SqliteSavingsGoalRepository SavingsGoals { get; }

    public SqliteSettingsRepository Settings { get; }

    public SqliteStatementImportRepository StatementImports { get; }

    public SqliteCategoryLearningRuleRepository CategoryLearningRules { get; }

    public ValueTask DisposeAsync()
    {
        DeleteIfExists(databasePath);
        DeleteIfExists($"{databasePath}-shm");
        DeleteIfExists($"{databasePath}-wal");

        return ValueTask.CompletedTask;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
