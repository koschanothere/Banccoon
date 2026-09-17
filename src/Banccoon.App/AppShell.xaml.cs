using Banccoon.App.ViewModels;

namespace Banccoon.App;

public partial class AppShell : Shell
{
    private readonly AppShellViewModel viewModel;

    public AppShell(AppShellViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Navigated += OnNavigated;
        _ = viewModel.RefreshFavoritesAsync();
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        _ = viewModel.RefreshFavoritesAsync();
    }
}
