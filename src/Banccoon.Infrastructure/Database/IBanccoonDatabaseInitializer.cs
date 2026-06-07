namespace Banccoon.Infrastructure.Database;

public interface IBanccoonDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
