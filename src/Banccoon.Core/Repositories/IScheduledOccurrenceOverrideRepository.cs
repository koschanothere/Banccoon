using Banccoon.Core.Models;

namespace Banccoon.Core.Repositories;

public interface IScheduledOccurrenceOverrideRepository
{
    Task<IReadOnlyList<ScheduledOccurrenceOverride>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ScheduledOccurrenceOverride occurrenceOverride, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
