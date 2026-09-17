using System.Windows.Input;
using Banccoon.Core.Appearance;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsRepository settingsRepository;
    private AppThemeMode themeMode = AppThemeMode.Light;

    public SettingsViewModel(ISettingsRepository settingsRepository)
    {
        this.settingsRepository = settingsRepository;
        SetLightCommand = new RelayCommand(() => _ = SetThemeModeAsync(AppThemeMode.Light));
        SetDarkCommand = new RelayCommand(() => _ = SetThemeModeAsync(AppThemeMode.Dark));
        SetSystemCommand = new RelayCommand(() => _ = SetThemeModeAsync(AppThemeMode.System));
    }

    public AppThemeMode ThemeMode
    {
        get => themeMode;
        private set
        {
            if (SetProperty(ref themeMode, value))
            {
                OnPropertyChanged(nameof(IsLightSelected));
                OnPropertyChanged(nameof(IsDarkSelected));
                OnPropertyChanged(nameof(IsSystemSelected));
            }
        }
    }

    public bool IsLightSelected => ThemeMode == AppThemeMode.Light;

    public bool IsDarkSelected => ThemeMode == AppThemeMode.Dark;

    public bool IsSystemSelected => ThemeMode == AppThemeMode.System;

    public ICommand SetLightCommand { get; }

    public ICommand SetDarkCommand { get; }

    public ICommand SetSystemCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken);
        ThemeMode = settings.ThemeMode;
        ApplyTheme(settings.ThemeMode);
    }

    private async Task SetThemeModeAsync(AppThemeMode mode)
    {
        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { ThemeMode = mode });
        ThemeMode = mode;
        ApplyTheme(mode);
    }

    private static void ApplyTheme(AppThemeMode mode)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.UserAppTheme = mode switch
        {
            AppThemeMode.Light => AppTheme.Light,
            AppThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
