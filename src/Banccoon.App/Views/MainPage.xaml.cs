using Banccoon.App.ViewModels;

namespace Banccoon.App.Views;

public partial class MainPage : ContentPage
{
    private readonly ShellViewModel viewModel;

    public MainPage(ShellViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
    }
}
