namespace Banccoon.Core.Appearance;

public sealed record UiPreferences(
    AppThemeMode ThemeMode,
    AccentColor AccentColor,
    NavigationStyle NavigationStyle,
    bool ShowPowerUserFeatures)
{
    public static UiPreferences Default { get; } = new(
        AppThemeMode.System,
        AccentColor.Emerald,
        NavigationStyle.Rail,
        ShowPowerUserFeatures: false);
}
