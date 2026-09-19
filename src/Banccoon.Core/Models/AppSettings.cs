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
    DateTimeOffset? LastAutoBackupAt = null)
{
    public UiPreferences UiPreferences => new(
        ThemeMode,
        AccentColor,
        NavigationStyle,
        ShowPowerUserFeatures);
}
