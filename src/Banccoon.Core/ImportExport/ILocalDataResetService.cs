namespace Banccoon.Core.ImportExport;

public interface ILocalDataResetService
{
    Task ResetAllAsync(CancellationToken cancellationToken = default);
}
