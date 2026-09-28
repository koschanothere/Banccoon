using Banccoon.Core.Appearance;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Models;

namespace Banccoon.Core.Setup;

// What the first-run setup screen collected. Category names are already in the chosen language
// (the App translates DefaultCategories keys); a tree two levels deep.
public sealed record FirstRunSetupRequest(
    string Language,
    string Currency,
    AppThemeMode ThemeMode,
    bool AutoBackupEnabled,
    int AutoBackupFrequencyDays,
    FreeToSpendWindowMode FreeToSpendWindowMode,
    IReadOnlyList<SetupCategory> Categories,
    IReadOnlyList<string> PreferredParserIds);

// A top-level category to create, with its children. IsFallback marks the import's catch-all
// ("Other"); its children aren't fallbacks.
public sealed record SetupCategory(
    string Name,
    TransactionType Type,
    CategoryColor? Color,
    IReadOnlyList<SetupCategory> Children,
    bool IsFallback = false);
