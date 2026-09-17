using Banccoon.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Banccoon.App;

public partial class AppShell : Shell
{
    private readonly AppShellViewModel viewModel;

    public AppShell()
    {
        InitializeComponent();
        viewModel = IPlatformApplication.Current!.Services.GetRequiredService<AppShellViewModel>();
        BindingContext = viewModel;
        Navigated += OnNavigated;
        _ = viewModel.RefreshFavoritesAsync();
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        _ = viewModel.RefreshFavoritesAsync();
    }
}
