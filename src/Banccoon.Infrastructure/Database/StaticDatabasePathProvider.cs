namespace Banccoon.Infrastructure.Database;

public sealed class StaticDatabasePathProvider : IDatabasePathProvider
{
    public StaticDatabasePathProvider(string databasePath)
    {
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }
}
