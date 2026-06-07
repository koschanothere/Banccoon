namespace Banccoon.Core.Models;

public sealed record Account(
    Guid Id,
    string Name,
    AccountType Type,
    decimal CurrentBalance,
    string Currency,
    DateTimeOffset CreatedDate,
    bool IsArchived = false,
    CreditCardDetails? CreditCardDetails = null);
