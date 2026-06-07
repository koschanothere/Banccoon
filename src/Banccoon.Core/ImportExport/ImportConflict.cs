namespace Banccoon.Core.ImportExport;

public sealed record ImportConflict(
    string EntityType,
    Guid EntityId,
    string Message);
