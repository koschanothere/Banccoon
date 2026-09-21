namespace Banccoon.Core.Models;

public sealed record ScheduledOccurrenceOverride(
    Guid Id,
    Guid ScheduledTransactionId,
    DateOnly OriginalOccurrenceDate,
    ScheduledOccurrenceOverrideKind Kind,
    DateOnly? DelayedToDate = null);
