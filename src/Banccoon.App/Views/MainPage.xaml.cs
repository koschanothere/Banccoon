using Banccoon.App.ViewModels;

namespace Banccoon.App.Views;

public partial class MainPage : ContentPage
{
    public MainPage(ShellViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
