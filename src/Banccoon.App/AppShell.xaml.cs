using Banccoon.App.Diagnostics;
using Banccoon.App.Formatting;
using Banccoon.App.ViewModels;
using Banccoon.App.Views;
using Banccoon.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Banccoon.App;

public partial class AppShell : Shell
{
    private readonly AppShellViewModel viewModel;
    private readonly ISettingsRepository settingsRepository;
    private bool hasCheckedAppLockOnStartup;

    public AppShell()
    {
        InitializeComponent();
        viewModel = IPlatformApplication.Current!.Services.GetRequiredService<AppShellViewModel>();
        settingsRepository = IPlatformApplication.Current!.Services.GetRequiredService<ISettingsRepository>();
        BindingContext = viewModel;
        Navigated += OnNavigated;
        _ = viewModel.RefreshFavoritesAsync();

        Routing.RegisterRoute("statementImport", typeof(StatementImportPage));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (hasCheckedAppLockOnStartup)
        {
            return;
        }

        hasCheckedAppLockOnStartup = true;
        _ = CheckAppLockOnStartupAsync();
    }

    private async Task CheckAppLockOnStartupAsync()
    {
        try
        {
            var settings = await settingsRepository.GetAsync();
            if (settings.AppLockPinHash is not null)
            {
                await ShowLockScreenAsync();
            }
        }
        catch (Exception ex)
        {
            // The app lock is a privacy speedbump, never a reason the app fails to open at all -
            // if anything here goes wrong, open unlocked rather than leaving the user stranded.
            DiagnosticLog.Write($"CheckAppLockOnStartupAsync failed, opening unlocked: {ex}");
        }
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        _ = viewModel.RefreshFavoritesAsync();
    }

    private async Task ShowLockScreenAsync()
    {
        var lockPage = IPlatformApplication.Current!.Services.GetRequiredService<AppLockPage>();
        await Navigation.PushModalAsync(lockPage, animated: false);
    }
}
