using Banccoon.App.ViewModels;
using Banccoon.Core.Models;

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
        viewModel.AddGoalRequested += OnAddGoalRequestedAsync;
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

    // Opens Accounts' own add-account form, preset to a Goal - see AccountsPage.ApplyQueryAttributes.
    private static Task OnAddGoalRequestedAsync()
    {
        return Shell.Current.GoToAsync($"//accounts?addAccountType={AccountType.Goal}");
    }
}
