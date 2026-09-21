using Banccoon.App.ViewModels;

namespace Banccoon.App.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel viewModel;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.CategoryDrillDownRequested += OnCategoryDrillDownRequestedAsync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.InitializeAsync();
    }

    private static Task OnCategoryDrillDownRequestedAsync(Guid? categoryId)
    {
        var route = categoryId is { } id ? $"//transactions?categoryId={id}" : "//transactions";
        return Shell.Current.GoToAsync(route);
    }
}
