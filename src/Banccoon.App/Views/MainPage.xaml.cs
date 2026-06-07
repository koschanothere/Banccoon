using Banccoon.App.ViewModels;

namespace Banccoon.App.Views;

public partial class MainPage : ContentPage
{
    public MainPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
