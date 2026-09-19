using Banccoon.App.ViewModels;

namespace Banccoon.App.Views;

public partial class TransactionsPage : ContentPage, IQueryAttributable
{
    private readonly TransactionsViewModel viewModel;

    public TransactionsPage(TransactionsViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    // Shell calls this before OnAppearing, when navigated here from the Dashboard's Analytics
    // "drill down into this category" action, or from an account's detail card on Accounts.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("categoryId", out var categoryValue) && Guid.TryParse(categoryValue?.ToString(), out var categoryId))
        {
            viewModel.SetPendingCategoryFilter(categoryId);
        }

        if (query.TryGetValue("accountId", out var accountValue) && Guid.TryParse(accountValue?.ToString(), out var accountId))
        {
            viewModel.SetPendingAccountFilter(accountId);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.InitializeAsync();
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("statementImport");
    }
}
