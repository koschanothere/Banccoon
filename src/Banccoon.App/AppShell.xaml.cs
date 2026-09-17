using Banccoon.App.ViewModels;
using Banccoon.App.Views;
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

        Routing.RegisterRoute("statementImport", typeof(StatementImportPage));
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        _ = viewModel.RefreshFavoritesAsync();
    }
}
