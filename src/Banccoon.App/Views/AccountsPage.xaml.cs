using Banccoon.App.ViewModels;
using Banccoon.Core.Models;

namespace Banccoon.App.Views;

public partial class AccountsPage : ContentPage, IQueryAttributable
{
    private readonly AccountsViewModel viewModel;

    public AccountsPage(AccountsViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.ViewTransactionsRequested += OnViewTransactionsRequestedAsync;
    }

    // Shell calls this before OnAppearing, when navigated here from the Dashboard Goals widget's
    // "+ Add goal" ("addAccountType=Goal") - the add form then opens preset to that type.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("addAccountType", out var typeValue) && Enum.TryParse<AccountType>(typeValue?.ToString(), out var type))
        {
            viewModel.SetPendingAddAccountType(type);
        }
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
