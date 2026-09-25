using Banccoon.App.ViewModels;

namespace Banccoon.App.Views;

public partial class StatementImportPage : ContentPage
{
    private readonly StatementImportViewModel viewModel;

    public StatementImportPage(StatementImportViewModel viewModel)
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

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    // "../reconciliation" pops this page and pushes the check-in in its place, so finishing the
    // check-in returns to Transactions rather than back into a finished import.
    private async void OnCheckInClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"../reconciliation?accountId={viewModel.Review.AccountId}");
    }
}
