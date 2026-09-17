using Banccoon.Core.Appearance;
using Banccoon.Core.Repositories;

namespace Banccoon.App;

public partial class App : Application
{
    public App(AppShell appShell, ISettingsRepository settingsRepository)
    {
        InitializeComponent();
        MainPage = appShell;
        _ = ApplyStartupThemeAsync(settingsRepository);
    }

    private async Task ApplyStartupThemeAsync(ISettingsRepository settingsRepository)
    {
        var settings = await settingsRepository.GetAsync();
        UserAppTheme = settings.ThemeMode switch
        {
            AppThemeMode.Light => AppTheme.Light,
            AppThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
