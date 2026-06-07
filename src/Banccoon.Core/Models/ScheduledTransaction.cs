using Banccoon.Core.Recurrence;

namespace Banccoon.Core.Models;

public sealed record ScheduledTransaction(
    Guid Id,
    string Name,
    decimal Amount,
    Guid AccountId,
    Guid? CategoryId,
    TransactionType Type,
    RecurrenceRule RecurrenceRule,
    DateOnly NextOccurrence,
    bool Active);
