using Banccoon.App.ViewModels;

namespace Banccoon.App.Views;

public partial class AccountsPage : ContentPage
{
    private readonly AccountsViewModel viewModel;

    public AccountsPage(AccountsViewModel viewModel)
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
}
