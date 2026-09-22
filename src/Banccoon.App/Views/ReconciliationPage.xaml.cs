using Banccoon.App.ViewModels;

namespace Banccoon.App.Views;

public partial class ReconciliationPage : ContentPage, IQueryAttributable
{
    private readonly ReconciliationViewModel viewModel;
    private Guid? fromStatementAccountId;

    public ReconciliationPage(ReconciliationViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    // Shell calls this before OnAppearing. "accountId" is passed when arriving straight from a
    // completed statement import (StatementImportPage's "Check in now"); the manual "Check in"
    // button on Transactions passes nothing.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        fromStatementAccountId = query.TryGetValue("accountId", out var accountValue) && Guid.TryParse(accountValue?.ToString(), out var accountId)
            ? accountId
            : null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.InitializeAsync(fromStatementAccountId);
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
