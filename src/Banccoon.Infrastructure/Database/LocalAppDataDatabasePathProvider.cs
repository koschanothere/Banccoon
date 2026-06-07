namespace Banccoon.Infrastructure.Database;

public sealed class LocalAppDataDatabasePathProvider : IDatabasePathProvider
{
    public string DatabasePath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Banccoon", "banccoon.db");
        }
    }
}
