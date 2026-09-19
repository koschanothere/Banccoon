using Banccoon.App.Formatting;
using Banccoon.App.ViewModels;

namespace Banccoon.App.Views;

public partial class AppLockPage : ContentPage
{
    private readonly AppLockViewModel viewModel;

    public AppLockPage(AppLockViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.Unlocked += OnUnlocked;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        PinEntry.Focus();
    }

    private void OnPinCompleted(object? sender, EventArgs e)
    {
        if (viewModel.UnlockCommand.CanExecute(null))
        {
            viewModel.UnlockCommand.Execute(null);
        }
    }

    private async void OnUnlocked()
    {
        AppLockState.IsLockScreenActive = false;
        AppLockState.RecordActivity();
        await Navigation.PopModalAsync();
    }
}
