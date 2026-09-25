using GB.MikroAdmin.ViewModels;

namespace GB.MikroAdmin.Views;

public partial class InterfacesPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;

    public InterfacesPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}
