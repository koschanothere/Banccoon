using Microsoft.Data.Sqlite;

namespace Banccoon.Infrastructure.Database;

public interface ISqliteConnectionFactory
{
    Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
