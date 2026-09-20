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

        // Tried enabling pooling to cut connection-open overhead, but it breaks anything that
        // needs the database file fully released right after use (confirmed by 31 test failures:
        // a pooled connection keeps the native file handle open past Dispose(), so an immediate
        // File.Delete of the db file threw "being used by another process" - the same shape of
        // problem restore-with-replace/delete-all-data would hit against the real database file).
        // Left disabled; the Cached* repository decorators (see EntityCache) already eliminate
        // almost all repeat connection opens, which is where the actual win was.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePathProvider.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        return connection;
    }
}
