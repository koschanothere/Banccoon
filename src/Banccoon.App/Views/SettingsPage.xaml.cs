using Banccoon.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Banccoon.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.InitializeAsync();
    }

    // DEV ONLY (see SettingsViewModel.IsDevToolsVisible): reopen first-run setup. It can be closed
    // unsaved from here; finishing it only adds categories that don't exist yet.
    private async void OnOpenFirstRunSetupClicked(object? sender, EventArgs e)
    {
        var setupPage = IPlatformApplication.Current!.Services.GetRequiredService<FirstRunSetupPage>();
        setupPage.CanClose = true;
        await Navigation.PushModalAsync(setupPage, animated: false);
    }
}
