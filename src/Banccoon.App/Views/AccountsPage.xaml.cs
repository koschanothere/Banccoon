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
        viewModel.ViewTransactionsRequested += OnViewTransactionsRequestedAsync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.InitializeAsync();
    }

    private static Task OnViewTransactionsRequestedAsync(Guid accountId)
    {
        return Shell.Current.GoToAsync($"//transactions?accountId={accountId}");
    }
}
