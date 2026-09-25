using GB.MikroAdmin.ViewModels;

namespace GB.MikroAdmin.Views;

public partial class MainMenuPage : ContentPage
{
    public MainMenuPage(MainMenuViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
