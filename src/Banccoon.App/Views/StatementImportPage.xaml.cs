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
}
