using Banccoon.Core.Appearance;
using Xunit;

namespace Banccoon.Tests.Appearance;

public sealed class UiPreferencesTests
{
    [Fact]
    public void Default_UsesFriendlySystemRailExperience()
    {
        var preferences = UiPreferences.Default;

        Assert.Equal(AppThemeMode.System, preferences.ThemeMode);
        Assert.Equal(AccentColor.Emerald, preferences.AccentColor);
        Assert.Equal(NavigationStyle.Rail, preferences.NavigationStyle);
        Assert.False(preferences.ShowPowerUserFeatures);
    }
}
