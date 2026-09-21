using Banccoon.Core.Models;

namespace Banccoon.App.Services;

public interface IAutoBackupRunner
{
    Task RunIfDueAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
