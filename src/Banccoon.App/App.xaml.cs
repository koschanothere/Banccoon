using Banccoon.App.Localization;
using Banccoon.Core.Appearance;
using Banccoon.Core.Repositories;

namespace Banccoon.App;

public partial class App : Application
{
    public App(ISettingsRepository settingsRepository)
    {
        InitializeComponent();
        MainPage = new AppShell();
        _ = ApplyStartupPreferencesAsync(settingsRepository);
    }

    private async Task ApplyStartupPreferencesAsync(ISettingsRepository settingsRepository)
    {
        var settings = await settingsRepository.GetAsync();
        UserAppTheme = settings.ThemeMode switch
        {
            AppThemeMode.Light => AppTheme.Light,
            AppThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

        Translator.SetLanguage(settings.DisplayLanguage);
    }
}
