namespace Banccoon.Core.Appearance;

public sealed record UiPreferences(
    AppThemeMode ThemeMode,
    AccentColor AccentColor,
    NavigationStyle NavigationStyle)
{
    public static UiPreferences Default { get; } = new(
        AppThemeMode.Light,
        AccentColor.Emerald,
        NavigationStyle.Rail);
}
