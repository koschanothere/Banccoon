using Banccoon.Core.Appearance;

namespace Banccoon.Core.Models;

public sealed record Category(
    Guid Id,
    string Name,
    TransactionType? Type = null,
    CategoryColor? Color = null);
