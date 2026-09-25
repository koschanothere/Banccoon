using Banccoon.App.Localization;
using Banccoon.Core.Appearance;
using Banccoon.Core.Repositories;

namespace Banccoon.App;

public partial class App : Application
{
    public App(ISettingsRepository settingsRepository)
    {
        InitializeComponent();
        _ = ApplyStartupPreferencesAsync(settingsRepository);
    }

    // MAUI's replacement for setting MainPage in the constructor (obsolete, warning CS0618). The
    // shell is created here, never constructor-injected into App.
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
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
