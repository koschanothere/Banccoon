using Microsoft.Data.Sqlite;

namespace Banccoon.Infrastructure.Database;

public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly IDatabasePathProvider databasePathProvider;

    public SqliteConnectionFactory(IDatabasePathProvider databasePathProvider)
    {
        this.databasePathProvider = databasePathProvider;
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var databaseDirectory = Path.GetDirectoryName(databasePathProvider.DatabasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePathProvider.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            Pooling = false
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        return connection;
    }
}
