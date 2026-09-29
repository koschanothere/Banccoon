using Banccoon.App.Diagnostics;
using Banccoon.App.ViewModels;
using Banccoon.Core.Models;

namespace Banccoon.App.Views;

// Shown modally (Navigation.PushModalAsync), like AppLockPage: a routed Shell page would leave the
// sidebar clickable underneath. Never constructor-injected into App/AppShell - AppShell resolves
// it from the service provider when it's needed.
public partial class FirstRunSetupPage : ContentPage
{
    private readonly FirstRunSetupViewModel viewModel;

    public FirstRunSetupPage(FirstRunSetupViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.Finished += OnFinished;
        viewModel.Closed += OnClosed;
        _ = viewModel.InitializeAsync();
    }

    // Opened from Settings (the dev button) rather than at startup: can be closed unsaved.
    public bool CanClose
    {
        get => viewModel.CanClose;
        set => viewModel.CanClose = value;
    }

    private async void OnFinished(FirstRunSetupDestination destination)
    {
        try
        {
            await Navigation.PopModalAsync(animated: false);
            switch (destination)
            {
                case FirstRunSetupDestination.StatementImport:
                    await Shell.Current.GoToAsync("//transactions");
                    await Shell.Current.GoToAsync("statementImport");
                    break;
                case FirstRunSetupDestination.AddAccount:
                    await Shell.Current.GoToAsync($"//accounts?addAccountType={AccountType.DebitCard}");
                    break;
                default:
                    await Shell.Current.GoToAsync("//dashboard");
                    break;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"Leaving first-run setup failed: {ex}");
        }
    }

    private async void OnClosed()
    {
        await Navigation.PopModalAsync(animated: false);
    }
}
