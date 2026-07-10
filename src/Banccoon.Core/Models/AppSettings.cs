using Banccoon.Core.Forecasting;
using Banccoon.Core.Appearance;

namespace Banccoon.Core.Models;

public sealed record AppSettings(
    string DefaultCurrency,
    ForecastPeriod DefaultForecastPeriod,
    ReminderFrequency ReminderFrequency,
    DateDisplayFormat DateDisplayFormat = DateDisplayFormat.DayMonthYear,
    AppThemeMode ThemeMode = AppThemeMode.Light,
    AccentColor AccentColor = AccentColor.Emerald,
    NavigationStyle NavigationStyle = NavigationStyle.Rail,
    bool ShowPowerUserFeatures = false)
{
    public UiPreferences UiPreferences => new(
        ThemeMode,
        AccentColor,
        NavigationStyle,
        ShowPowerUserFeatures);
}
