using Banccoon.Core.Forecasting;
using Banccoon.Core.Appearance;

namespace Banccoon.Core.Models;

public sealed record AppSettings(
    string DefaultCurrency,
    ForecastPeriod DefaultForecastPeriod,
    ReminderFrequency ReminderFrequency,
    DateDisplayFormat DateDisplayFormat = DateDisplayFormat.DayMonthYear,
    AppThemeMode ThemeMode = AppThemeMode.System,
    AccentColor AccentColor = AccentColor.Emerald,
    NavigationStyle NavigationStyle = NavigationStyle.Rail,
    bool ShowPowerUserFeatures = false,
    FreeToSpendWindowMode FreeToSpendWindowMode = FreeToSpendWindowMode.RollingDays,
    int FreeToSpendWindowDays = 7,
    decimal SafetyBuffer = 0m,
    decimal MajorPaymentThreshold = 0m,
    int ResolveUpcomingNearTermDays = 3,
    Guid? PrimaryAccountId = null,
    string DashboardSectionOrder = "Upcoming,Analytics,Goals",
    string DisplayLanguage = "en",
    AccountType DefaultAccountType = AccountType.DebitCard,
    bool PrivacyModeEnabled = false,
    string? AppLockPinHash = null,
    string? AppLockPinSalt = null,
    int AppLockAutoLockMinutes = 5,
    bool AutoBackupEnabled = false,
    int AutoBackupFrequencyDays = 30,
    int AutoBackupRetentionCount = 5,
    DateTimeOffset? LastAutoBackupAt = null,
    DashboardPrimaryMetric DashboardPrimaryMetric = DashboardPrimaryMetric.FreeToSpend,
    // Set once LegacySavingsGoalConversionService has turned the old standalone SavingsGoal rows
    // into Goal accounts, so that conversion only ever runs once.
    bool LegacySavingsGoalsConverted = false,
    // Set once first-run setup has finished (or a backup was restored from it). Setup shows only
    // while this is false AND nothing has been created yet - see FirstRunSetupService.
    bool FirstRunCompleted = false,
    // The banks (statement parser ids, comma-separated) chosen at setup: tried first when reading
    // a statement, before detecting across every parser. Empty = detect only.
    string PreferredParserIds = "",
    // The catch-all category an import approval with no category is filed under ("Other" /
    // "Прочее", created by setup in the chosen language). Null falls back to a category named
    // "Other" - see StatementImportService.
    Guid? FallbackCategoryId = null,
    // Buttons show their Heroicons glyph (most as icon + tooltip); off falls back to text-only
    // buttons everywhere. A display preference, deliberately not asked at first-run setup.
    bool ShowIcons = true)
{
    public UiPreferences UiPreferences => new(
        ThemeMode,
        AccentColor,
        NavigationStyle,
        ShowPowerUserFeatures);
}
