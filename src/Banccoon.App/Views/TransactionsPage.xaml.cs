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
    // "drill down into this category" action (see DashboardPage.OnCategoryDrillDownRequestedAsync).
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("categoryId", out var value) && Guid.TryParse(value?.ToString(), out var categoryId))
        {
            viewModel.SetPendingCategoryFilter(categoryId);
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
