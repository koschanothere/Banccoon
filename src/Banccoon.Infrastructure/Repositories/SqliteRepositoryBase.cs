using Banccoon.Infrastructure.Database;
using Microsoft.Data.Sqlite;

namespace Banccoon.Infrastructure.Repositories;

public abstract class SqliteRepositoryBase
{
    private readonly IBanccoonDatabaseInitializer databaseInitializer;

    protected SqliteRepositoryBase(
        ISqliteConnectionFactory connectionFactory,
        IBanccoonDatabaseInitializer databaseInitializer)
    {
        ConnectionFactory = connectionFactory;
        this.databaseInitializer = databaseInitializer;
    }

    protected ISqliteConnectionFactory ConnectionFactory { get; }

    protected Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        return databaseInitializer.InitializeAsync(cancellationToken);
    }

    protected static void AddParameter(SqliteCommand command, string name, object value)
    {
        command.Parameters.AddWithValue(name, value);
    }
}
