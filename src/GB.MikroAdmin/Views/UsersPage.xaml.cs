using GB.MikroAdmin.ViewModels;

namespace GB.MikroAdmin.Views;

public partial class UsersPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;

    public UsersPage(DashboardViewModel viewModel)
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
